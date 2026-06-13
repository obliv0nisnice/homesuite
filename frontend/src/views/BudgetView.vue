<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { apiFetch } from '@/services/api'

// ─── Types ────────────────────────────────────────────────────────────────────

type Category = {
  id: string
  name: string
  type: string
  monthlyLimit?: number | null
}

type Transaction = {
  id: string
  title: string
  amount: number
  date: string
  note?: string | null
  categoryId: string
  categoryName: string
  categoryType: string
  isRecurring?: boolean
  recurringInterval?: 'monthly' | 'weekly' | 'quarterly' | 'yearly' | null
  nextDueDate?: string | null
}

type CategorySummary = {
  id: string
  name: string
  icon: string
  amount: number
  limit: number
  hasCustomLimit: boolean
  color: string
}

// ─── Constants ────────────────────────────────────────────────────────────────

const categoryPalette = [
  '#6366f1', '#f59e0b', '#ec4899', '#8b5cf6', '#ef4444', '#06b6d4', '#f97316', '#a855f7',
]
const incomePalette = ['#10b981', '#34d399', '#059669', '#6ee7b7']
const categoryIcons = ['🏠', '🛒', '🚗', '🎉', '💊', '📦', '🍽️', '📚']

// ─── State ────────────────────────────────────────────────────────────────────

const categories = ref<Category[]>([])
const transactions = ref<Transaction[]>([])
const loading = ref(false)
const error = ref('')
const success = ref('')

const showAddModal = ref(false)
const showRecurringModal = ref(false)
const showEditModal = ref(false)
const showBudgetLimitModal = ref(false)

const deletingId = ref<string | null>(null)
const stoppingRecurringId = ref<string | null>(null)
const deletingCategoryId = ref<string | null>(null)
const savingEdit = ref(false)

const editingLimits = ref<Record<string, number | null>>({})
const savingLimits = ref(false)

const selectedMonth = ref(startOfMonth(new Date()))

const txSearch = ref('')
const txTypeFilter = ref<'all' | 'Income' | 'Expense'>('all')
const txPage = ref(1)
const TX_PAGE_SIZE = 20

const editTransaction = ref<{
  id: string
  title: string
  type: string
  amount: number
  date: string
  note: string
  categoryId: string
  isRecurring: boolean
  recurringInterval: 'monthly' | 'weekly' | 'quarterly' | 'yearly'
} | null>(null)

const newCategory = ref({ name: '', type: 'Expense' })

const newTransaction = ref({
  title: '',
  type: 'Expense',
  amount: 0,
  date: new Date().toISOString().slice(0, 10),
  note: '',
  categoryId: '',
  isRecurring: false,
  recurringInterval: 'monthly' as 'monthly' | 'weekly' | 'quarterly' | 'yearly',
})

const donutRef = ref<HTMLCanvasElement | null>(null)
const barRef = ref<HTMLCanvasElement | null>(null)

// ─── Computed ─────────────────────────────────────────────────────────────────

// Vorlagen (isRecurring) sind keine Buchungen — die Monats-Buchungen entstehen
// als eigene Zeilen und sind dadurch einzeln (nur für den Monat) bearbeitbar.
const monthTransactions = computed(() =>
  transactions.value.filter((tx) => !tx.isRecurring && isInSelectedMonth(tx.date)),
)

const expenseCategories = computed<CategorySummary[]>(() =>
  categories.value
    .filter((c) => c.type === 'Expense')
    .map((category, index) => {
      const spent = monthTransactions.value
        .filter((tx) => tx.categoryId === category.id && tx.categoryType === 'Expense')
        .reduce((sum, tx) => sum + Math.abs(tx.amount), 0)
      const customLimit = category.monthlyLimit ?? undefined
      const derivedLimit = customLimit ?? (spent > 0 ? Math.ceil((spent * 1.15) / 10) * 10 : 100)
      return {
        id: category.id,
        name: category.name,
        icon: categoryIcons[index % categoryIcons.length] ?? '📦',
        amount: Number(spent.toFixed(2)),
        limit: derivedLimit,
        hasCustomLimit: customLimit != null,
        color: categoryPalette[index % categoryPalette.length] ?? '#6366f1',
      }
    }),
)

const incomeCategories = computed<CategorySummary[]>(() =>
  categories.value
    .filter((c) => c.type === 'Income')
    .map((category, index) => {
      const earned = monthTransactions.value
        .filter((tx) => tx.categoryId === category.id && tx.categoryType === 'Income')
        .reduce((sum, tx) => sum + Math.abs(tx.amount), 0)
      return {
        id: category.id,
        name: category.name,
        icon: '💵',
        amount: Number(earned.toFixed(2)),
        limit: 0,
        hasCustomLimit: false,
        color: incomePalette[index % incomePalette.length] ?? '#10b981',
      }
    }),
)

const filteredTransactions = computed(() => {
  const search = txSearch.value.trim().toLowerCase()

  return [...monthTransactions.value]
    .filter((tx) => txTypeFilter.value === 'all' || tx.categoryType === txTypeFilter.value)
    .filter((tx) =>
      !search ||
      tx.title.toLowerCase().includes(search) ||
      (tx.note ?? '').toLowerCase().includes(search) ||
      tx.categoryName.toLowerCase().includes(search))
    .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
})

const txTotalPages = computed(() =>
  Math.max(1, Math.ceil(filteredTransactions.value.length / TX_PAGE_SIZE)),
)

const pagedTransactions = computed(() => {
  const page = Math.min(txPage.value, txTotalPages.value)
  return filteredTransactions.value.slice((page - 1) * TX_PAGE_SIZE, page * TX_PAGE_SIZE)
})

const recurringTransactions = computed(() =>
  transactions.value.filter((t) => t.isRecurring),
)

const totalIncome = computed(() =>
  monthTransactions.value
    .filter((t) => t.categoryType === 'Income')
    .reduce((sum, t) => sum + Math.abs(t.amount), 0),
)

const totalExpenses = computed(() =>
  monthTransactions.value
    .filter((t) => t.categoryType === 'Expense')
    .reduce((sum, t) => sum + Math.abs(t.amount), 0),
)

const balance = computed(() => totalIncome.value - totalExpenses.value)

const expensePercent = computed(() =>
  totalIncome.value > 0 ? Math.round((totalExpenses.value / totalIncome.value) * 100) : 0,
)

const savingsRate = computed(() =>
  totalIncome.value > 0
    ? Math.max(0, Math.round(((totalIncome.value - totalExpenses.value) / totalIncome.value) * 100))
    : 0,
)

const overBudgetCount = computed(() =>
  expenseCategories.value.filter((c) => c.amount > c.limit).length,
)

const selectedMonthLabel = computed(() =>
  selectedMonth.value.toLocaleString('de-AT', { month: 'long', year: 'numeric' }),
)

const isCurrentMonth = computed(() =>
  selectedMonth.value.getTime() === startOfMonth(new Date()).getTime(),
)

const filteredCategoriesForModal = computed(() =>
  categories.value.filter((c) => c.type === newTransaction.value.type),
)

const filteredCategoriesForEdit = computed(() =>
  editTransaction.value
    ? categories.value.filter((c) => c.type === editTransaction.value!.type)
    : [],
)

// ─── Helpers ──────────────────────────────────────────────────────────────────

function startOfMonth(d: Date) {
  return new Date(d.getFullYear(), d.getMonth(), 1)
}

function isInSelectedMonth(date: string) {
  const d = new Date(date)
  return d.getFullYear() === selectedMonth.value.getFullYear()
    && d.getMonth() === selectedMonth.value.getMonth()
}

function goPrevMonth() {
  selectedMonth.value = new Date(selectedMonth.value.getFullYear(), selectedMonth.value.getMonth() - 1, 1)
  txPage.value = 1
}

function goNextMonth() {
  if (isCurrentMonth.value) return
  selectedMonth.value = new Date(selectedMonth.value.getFullYear(), selectedMonth.value.getMonth() + 1, 1)
  txPage.value = 1
}

function formatNum(n: number) {
  return n.toLocaleString('de-AT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString('de-AT')
}

function getCatColor(categoryName: string, categoryType: string) {
  if (categoryType === 'Income') {
    return incomeCategories.value.find((c) => c.name === categoryName)?.color ?? '#10b981'
  }
  return expenseCategories.value.find((c) => c.name === categoryName)?.color ?? '#6366f1'
}

function isDarkMode() {
  return document.documentElement.getAttribute('data-bs-theme') === 'dark'
}

function intervalLabel(interval?: string | null) {
  if (interval === 'monthly') return 'Monatlich'
  if (interval === 'weekly') return 'Wöchentlich'
  if (interval === 'quarterly') return 'Quartalsweise'
  if (interval === 'yearly') return 'Jährlich'
  return ''
}

function intervalIcon(interval?: string | null) {
  if (interval === 'weekly') return '📅'
  if (interval === 'monthly') return '🗓️'
  if (interval === 'quarterly') return '📊'
  if (interval === 'yearly') return '📆'
  return '🔁'
}

// ─── Data Fetching ────────────────────────────────────────────────────────────

async function loadData() {
  loading.value = true
  error.value = ''
  try {
    const [loadedCategories, loadedTransactions] = await Promise.all([
      apiFetch<Category[]>('/categories'),
      apiFetch<Transaction[]>('/transactions'),
    ])
    categories.value = loadedCategories
    transactions.value = loadedTransactions
    if (!newTransaction.value.categoryId) {
      newTransaction.value.categoryId =
        loadedCategories.find((c) => c.type === newTransaction.value.type)?.id ??
        loadedCategories[0]?.id ?? ''
    }
    await migrateLocalBudgetLimits()
    await nextTick()
    renderCharts()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Daten konnten nicht geladen werden.'
  } finally {
    loading.value = false
  }
}

// ─── Actions ──────────────────────────────────────────────────────────────────

async function createCategory() {
  error.value = ''
  success.value = ''
  try {
    await apiFetch<Category>('/categories', { method: 'POST', body: JSON.stringify(newCategory.value) })
    newCategory.value = { name: '', type: 'Expense' }
    success.value = 'Kategorie wurde angelegt.'
    await loadData()
  } catch {
    error.value = 'Kategorie konnte nicht erstellt werden.'
  }
}

async function addTransaction() {
  error.value = ''
  success.value = ''
  try {
    await apiFetch<Transaction>('/transactions', {
      method: 'POST',
      body: JSON.stringify({
        title: newTransaction.value.title,
        amount: Number(newTransaction.value.amount),
        date: newTransaction.value.date,
        note: newTransaction.value.note,
        categoryId: newTransaction.value.categoryId,
        isRecurring: newTransaction.value.isRecurring,
        recurringInterval: newTransaction.value.isRecurring ? newTransaction.value.recurringInterval : null,
      }),
    })
    showAddModal.value = false
    newTransaction.value = {
      title: '', type: 'Expense', amount: 0,
      date: new Date().toISOString().slice(0, 10), note: '',
      categoryId: categories.value.find((c) => c.type === 'Expense')?.id ?? categories.value[0]?.id ?? '',
      isRecurring: false, recurringInterval: 'monthly',
    }
    success.value = 'Transaktion wurde angelegt.'
    await loadData()
  } catch {
    error.value = 'Transaktion konnte nicht erstellt werden.'
  }
}

const editingTemplate = ref(false)

function openEditModal(tx: Transaction) {
  editingTemplate.value = tx.isRecurring ?? false
  editTransaction.value = {
    id: tx.id,
    title: tx.title,
    type: tx.categoryType,
    amount: Math.abs(tx.amount),
    date: tx.date.slice(0, 10),
    note: tx.note ?? '',
    categoryId: tx.categoryId,
    isRecurring: tx.isRecurring ?? false,
    recurringInterval: (tx.recurringInterval as any) ?? 'monthly',
  }
  showEditModal.value = true
}

async function saveEdit() {
  if (!editTransaction.value) return
  savingEdit.value = true
  error.value = ''
  try {
    await apiFetch<Transaction>(`/transactions/${editTransaction.value.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        title: editTransaction.value.title,
        amount: Number(editTransaction.value.amount),
        date: editTransaction.value.date,
        note: editTransaction.value.note,
        categoryId: editTransaction.value.categoryId,
        isRecurring: editTransaction.value.isRecurring,
        recurringInterval: editTransaction.value.isRecurring ? editTransaction.value.recurringInterval : null,
      }),
    })
    showEditModal.value = false
    editTransaction.value = null
    success.value = 'Transaktion aktualisiert.'
    await loadData()
  } catch {
    error.value = 'Transaktion konnte nicht gespeichert werden.'
  } finally {
    savingEdit.value = false
  }
}

async function deleteTransaction(id: string) {
  if (!confirm('Transaktion wirklich löschen?')) return
  deletingId.value = id
  error.value = ''
  try {
    await apiFetch(`/transactions/${id}`, { method: 'DELETE' })
    transactions.value = transactions.value.filter((t) => t.id !== id)
    success.value = 'Transaktion gelöscht.'
    await nextTick()
    renderCharts()
  } catch {
    error.value = 'Transaktion konnte nicht gelöscht werden.'
  } finally {
    deletingId.value = null
  }
}

async function stopRecurring(tx: Transaction) {
  if (!confirm(`Wiederholung für „${tx.title}" beenden? Bereits gebuchte Monate bleiben erhalten.`)) return
  stoppingRecurringId.value = tx.id
  error.value = ''
  try {
    // Vorlagen sind keine Buchungen — Stoppen heißt: Vorlage löschen.
    await apiFetch(`/transactions/${tx.id}`, { method: 'DELETE' })
    transactions.value = transactions.value.filter((t) => t.id !== tx.id)
    success.value = `Wiederholung für „${tx.title}" gestoppt.`
  } catch {
    error.value = 'Wiederholung konnte nicht gestoppt werden.'
  } finally {
    stoppingRecurringId.value = null
  }
}

async function deleteCategory(id: string, name: string) {
  if (!confirm(`Kategorie „${name}" wirklich löschen?\nAchtung: Transaktionen bleiben erhalten.`)) return
  deletingCategoryId.value = id
  error.value = ''
  try {
    await apiFetch(`/categories/${id}`, { method: 'DELETE' })
    categories.value = categories.value.filter((c) => c.id !== id)
    success.value = `Kategorie „${name}" gelöscht.`
    await nextTick()
    renderCharts()
  } catch {
    error.value = 'Kategorie konnte nicht gelöscht werden. Möglicherweise noch Transaktionen verknüpft.'
  } finally {
    deletingCategoryId.value = null
  }
}

// ─── Budget Limits ────────────────────────────────────────────────────────────

function openBudgetLimitModal() {
  editingLimits.value = {}
  categories.value
    .filter((c) => c.type === 'Expense')
    .forEach((cat) => {
      editingLimits.value[cat.id] = cat.monthlyLimit ?? null
    })
  showBudgetLimitModal.value = true
}

async function saveBudgetLimits() {
  savingLimits.value = true
  error.value = ''
  try {
    for (const cat of categories.value.filter((c) => c.type === 'Expense')) {
      const rawLimit = editingLimits.value[cat.id]
      const newLimit = typeof rawLimit === 'number' && rawLimit > 0 ? rawLimit : null
      if ((cat.monthlyLimit ?? null) === newLimit) continue
      await apiFetch(`/categories/${cat.id}`, {
        method: 'PUT',
        body: JSON.stringify({ name: cat.name, type: cat.type, monthlyLimit: newLimit }),
      })
      cat.monthlyLimit = newLimit
    }
    showBudgetLimitModal.value = false
    success.value = 'Budget-Limits gespeichert.'
    await nextTick()
    renderCharts()
  } catch {
    error.value = 'Budget-Limits konnten nicht gespeichert werden.'
  } finally {
    savingLimits.value = false
  }
}

function resetBudgetLimits() {
  editingLimits.value = {}
  categories.value
    .filter((c) => c.type === 'Expense')
    .forEach((cat) => {
      editingLimits.value[cat.id] = null
    })
}

// Einmalige Übernahme der früher im localStorage gespeicherten Limits ins Backend.
async function migrateLocalBudgetLimits() {
  const stored = localStorage.getItem('budgetLimits')
  if (!stored) return
  try {
    const parsed = JSON.parse(stored) as Record<string, number>
    for (const cat of categories.value) {
      const limit = parsed[cat.id]
      if (cat.type === 'Expense' && typeof limit === 'number' && limit > 0 && cat.monthlyLimit == null) {
        await apiFetch(`/categories/${cat.id}`, {
          method: 'PUT',
          body: JSON.stringify({ name: cat.name, type: cat.type, monthlyLimit: limit }),
        })
        cat.monthlyLimit = limit
      }
    }
    localStorage.removeItem('budgetLimits')
  } catch {
    // localStorage-Inhalt unbrauchbar — Limits bleiben einfach ungesetzt.
  }
}

// ─── Charts ───────────────────────────────────────────────────────────────────

function renderDonut() {
  const canvas = donutRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const dark = isDarkMode()
  const W = 220, H = 220, cx = W / 2, cy = H / 2
  const outerR = 90, innerR = 60
  const ringWidth = outerR - innerR
  const arcRadius = innerR + ringWidth / 2
  const GAP = 0.035

  ctx.clearRect(0, 0, W, H)
  canvas.width = W
  canvas.height = H

  const incomeData = incomeCategories.value.filter((c) => c.amount > 0)
  const expenseData = expenseCategories.value.filter((c) => c.amount > 0)
  const totalInc = incomeData.reduce((s, c) => s + c.amount, 0)
  const totalExp = expenseData.reduce((s, c) => s + c.amount, 0)
  const grandTotal = totalInc + totalExp

  ctx.lineCap = 'butt'
  ctx.lineWidth = ringWidth

  if (grandTotal <= 0) {
    ctx.beginPath()
    ctx.strokeStyle = dark ? '#334155' : '#e2e8f0'
    ctx.arc(cx, cy, arcRadius, 0, Math.PI * 2)
    ctx.stroke()
    return
  }

  const incArcTotal = (totalInc / grandTotal) * Math.PI * 2
  const expArcTotal = (totalExp / grandTotal) * Math.PI * 2

  // Draw income slices (green zone)
  ctx.beginPath()
  ctx.strokeStyle = dark ? '#1e293b' : '#f8fafc'
  ctx.arc(cx, cy, arcRadius, 0, Math.PI * 2)
  ctx.stroke()

  const drawSegment = (start: number, end: number, color: string) => {
    if (end <= start) return
    ctx.beginPath()
    ctx.arc(cx, cy, arcRadius, start, end)
    ctx.strokeStyle = color
    ctx.shadowColor = `${color}55`
    ctx.shadowBlur = 8
    ctx.stroke()
  }

  let angle = -Math.PI / 2
  incomeData.forEach((cat) => {
    const slice = (cat.amount / grandTotal) * Math.PI * 2
    const start = angle + GAP / 2
    const end = angle + slice - GAP / 2
  // Draw a visible white gap arc between income and expense
    drawSegment(start, end, cat.color)
    angle += slice
  })

  if (totalInc > 0 && totalExp > 0) {
    angle = -Math.PI / 2 + incArcTotal
  }

  expenseData.forEach((cat) => {
    const slice = (cat.amount / grandTotal) * Math.PI * 2
    const start = angle + GAP / 2
    const end = angle + slice - GAP / 2
    drawSegment(start, end, cat.color)
    angle += slice
  })

  ctx.shadowBlur = 0

  const drawDiv = (a: number) => {
    ctx.beginPath()
    ctx.moveTo(cx + innerR * Math.cos(a), cy + innerR * Math.sin(a))
    ctx.lineTo(cx + outerR * Math.cos(a), cy + outerR * Math.sin(a))
    ctx.strokeStyle = '#ffffff'
    ctx.lineWidth = 4
    ctx.stroke()
  }

  if (totalInc > 0 && totalExp > 0) {
    drawDiv(-Math.PI / 2)
    drawDiv(-Math.PI / 2 + incArcTotal)
  }

  ctx.lineWidth = ringWidth

  const drawArcLabel = (text: string, midAngle: number) => {
    const r = arcRadius
    ctx.font = 'bold 8px system-ui'
    ctx.fillStyle = dark ? '#94a3b8' : '#64748b'
    ctx.textAlign = 'center'
    ctx.fillText(text, cx + r * Math.cos(midAngle), cy + r * Math.sin(midAngle) + 3)
  }
  if (totalInc > 0) drawArcLabel('IN', -Math.PI / 2 + incArcTotal / 2)
  if (totalExp > 0) drawArcLabel('EX', -Math.PI / 2 + incArcTotal + expArcTotal / 2)
}

function renderBar() {
  const canvas = barRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const dark = isDarkMode()
  const W = canvas.offsetWidth || 480, H = 240
  canvas.width = W; canvas.height = H
  ctx.clearRect(0, 0, W, H)

  const monthKeys = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(selectedMonth.value)
    d.setMonth(d.getMonth() - (5 - i))
    return { key: `${d.getFullYear()}-${d.getMonth()}`, label: d.toLocaleDateString('de-AT', { month: 'short' }) }
  })

  const income = monthKeys.map(({ key }) =>
    transactions.value.filter((tx) => { const d = new Date(tx.date); return `${d.getFullYear()}-${d.getMonth()}` === key && tx.categoryType === 'Income' })
      .reduce((sum, tx) => sum + Math.abs(tx.amount), 0))

  const expenses = monthKeys.map(({ key }) =>
    transactions.value.filter((tx) => { const d = new Date(tx.date); return `${d.getFullYear()}-${d.getMonth()}` === key && tx.categoryType === 'Expense' })
      .reduce((sum, tx) => sum + Math.abs(tx.amount), 0))

  const padL = 44, padR = 16, padT = 16, padB = 36
  const chartW = W - padL - padR, chartH = H - padT - padB
  const maxVal = Math.max(...income, ...expenses, 100) * 1.1
  const barW = (chartW / monthKeys.length) * 0.33
  const groupW = chartW / monthKeys.length

  for (let i = 0; i <= 4; i++) {
    const y = padT + chartH - (i / 4) * chartH
    ctx.beginPath(); ctx.strokeStyle = dark ? '#2d3f5740' : '#e2e8f060'; ctx.lineWidth = 1
    ctx.moveTo(padL, y); ctx.lineTo(W - padR, y); ctx.stroke()
    ctx.fillStyle = dark ? '#64748b' : '#94a3b8'; ctx.font = '10px system-ui, sans-serif'
    ctx.textAlign = 'right'; ctx.fillText('€' + Math.round((maxVal / 4) * i), padL - 6, y + 4)
  }

  monthKeys.forEach((month, i) => {
    const cx = padL + groupW * i + groupW / 2
    const incomeValue = income[i] ?? 0
    const expenseValue = expenses[i] ?? 0
    const incH = (incomeValue / maxVal) * chartH, expH = (expenseValue / maxVal) * chartH
    const g1 = ctx.createLinearGradient(0, padT + chartH - incH, 0, padT + chartH)
    g1.addColorStop(0, '#10b981'); g1.addColorStop(1, '#10b98155')
    ctx.fillStyle = g1; roundRect(ctx, cx - barW - 2, padT + chartH - incH, barW, incH, 4); ctx.fill()
    const g2 = ctx.createLinearGradient(0, padT + chartH - expH, 0, padT + chartH)
    g2.addColorStop(0, '#ef4444'); g2.addColorStop(1, '#ef444455')
    ctx.fillStyle = g2; roundRect(ctx, cx + 2, padT + chartH - expH, barW, expH, 4); ctx.fill()
    ctx.fillStyle = dark ? '#94a3b8' : '#64748b'; ctx.font = '11px system-ui, sans-serif'
    ctx.textAlign = 'center'; ctx.fillText(month.label, cx, H - 8)
  })
}

function roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, width: number, height: number, radius: number) {
  if (height <= 0) return
  ctx.beginPath(); ctx.moveTo(x + radius, y); ctx.lineTo(x + width - radius, y)
  ctx.quadraticCurveTo(x + width, y, x + width, y + radius); ctx.lineTo(x + width, y + height)
  ctx.lineTo(x, y + height); ctx.lineTo(x, y + radius); ctx.quadraticCurveTo(x, y, x + radius, y)
  ctx.closePath()
}

function renderCharts() { renderDonut(); renderBar() }

// ─── Lifecycle ────────────────────────────────────────────────────────────────

onMounted(async () => {
  await loadData()
  const observer = new MutationObserver(() => nextTick(() => renderCharts()))
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] })
})

watch(() => newTransaction.value.type, (type) => {
  const match = categories.value.find((c) => c.type === type)
  if (match) newTransaction.value.categoryId = match.id
})

watch(() => editTransaction.value?.type, (type) => {
  if (!type || !editTransaction.value) return
  const match = categories.value.find((c) => c.type === type)
  if (match) editTransaction.value.categoryId = match.id
})

watch([transactions, categories], async () => { await nextTick(); renderCharts() }, { deep: true })

watch(selectedMonth, async () => { await nextTick(); renderCharts() })

watch([txSearch, txTypeFilter], () => { txPage.value = 1 })
</script>

<template>
  <BContainer class="py-4 d-flex flex-column gap-3">

    <!-- Header -->
    <div class="d-flex justify-content-between align-items-end flex-wrap gap-3">
      <div>
        <h1 class="h2 fw-bold mb-1">Budget <span class="text-primary">Übersicht</span></h1>
        <div class="d-flex align-items-center gap-2">
          <BButton size="sm" variant="outline-secondary" title="Voriger Monat" @click="goPrevMonth">‹</BButton>
          <span class="fw-semibold" style="min-width: 130px; text-align: center;">{{ selectedMonthLabel }}</span>
          <BButton size="sm" variant="outline-secondary" title="Nächster Monat" :disabled="isCurrentMonth" @click="goNextMonth">›</BButton>
        </div>
      </div>
      <div class="d-flex gap-2 flex-wrap align-items-center">
        <BButton variant="outline-secondary" :disabled="loading" @click="loadData">↺ Neu laden</BButton>
        <BButton v-if="recurringTransactions.length > 0" variant="outline-secondary" @click="showRecurringModal = true">
          🔁 Wiederkehrend <BBadge variant="primary">{{ recurringTransactions.length }}</BBadge>
        </BButton>
        <BButton variant="primary" @click="showAddModal = true">＋ Transaktion</BButton>
      </div>
    </div>

    <!-- Alerts -->
    <BAlert :model-value="!!error" variant="danger">⚠ {{ error }}</BAlert>
    <BAlert :model-value="!!success" variant="success">✓ {{ success }}</BAlert>

    <!-- KPI Cards -->
    <BRow class="g-3">
      <BCol md="3" sm="6">
        <BCard>
          <div class="text-secondary text-uppercase small">💵 Einnahmen</div>
          <div class="fs-4 fw-bold">€ {{ formatNum(totalIncome) }}</div>
          <div class="text-success small">{{ selectedMonthLabel }}</div>
        </BCard>
      </BCol>
      <BCol md="3" sm="6">
        <BCard>
          <div class="text-secondary text-uppercase small">💸 Ausgaben</div>
          <div class="fs-4 fw-bold">€ {{ formatNum(totalExpenses) }}</div>
          <div class="text-danger small">{{ expensePercent }}% der Einnahmen</div>
        </BCard>
      </BCol>
      <BCol md="3" sm="6">
        <BCard>
          <div class="text-secondary text-uppercase small">🏦 Restbudget</div>
          <div class="fs-4 fw-bold">€ {{ formatNum(balance) }}</div>
          <div class="small" :class="balance >= 0 ? 'text-success' : 'text-danger'">
            {{ balance >= 0 ? '✓ Im grünen Bereich' : '⚠ Überzogen' }}
          </div>
        </BCard>
      </BCol>
      <BCol md="3" sm="6">
        <BCard>
          <div class="text-secondary text-uppercase small">🎯 Sparquote</div>
          <div class="fs-4 fw-bold">{{ savingsRate }}%</div>
          <div class="text-success small">Ziel: 20%</div>
        </BCard>
      </BCol>
    </BRow>

    <!-- Charts Row -->
    <BRow class="g-3">
      <BCol lg="4">
        <BCard>
          <div class="d-flex justify-content-between align-items-center mb-3">
            <span class="fw-bold">Verteilung</span>
            <BBadge variant="primary">{{ selectedMonthLabel }}</BBadge>
          </div>
          <div class="donut-wrapper">
            <canvas ref="donutRef" width="220" height="220"></canvas>
            <div class="donut-center">
              <span class="d-block fw-bold">€{{ formatNum(balance) }}</span>
              <span class="text-secondary small">Bilanz</span>
            </div>
          </div>
          <div class="d-flex flex-column gap-1 mt-2" style="max-height: 220px; overflow-y: auto;">
            <div class="text-secondary text-uppercase small fw-bold">Einnahmen</div>
            <div v-for="cat in incomeCategories.filter(c => c.amount > 0)" :key="'i-' + cat.id" class="d-flex align-items-center gap-2 small">
              <span class="rounded-circle" :style="{ background: cat.color, width: '8px', height: '8px' }"></span>
              <span class="flex-grow-1">{{ cat.name }}</span>
              <span class="fw-semibold text-success">+€{{ formatNum(cat.amount) }}</span>
            </div>
            <div v-if="!incomeCategories.some(c => c.amount > 0)" class="text-secondary fst-italic small">Keine Einnahmen</div>
            <div class="text-secondary text-uppercase small fw-bold mt-2">Ausgaben</div>
            <div v-for="cat in expenseCategories.filter(c => c.amount > 0)" :key="'e-' + cat.id" class="d-flex align-items-center gap-2 small">
              <span class="rounded-circle" :style="{ background: cat.color, width: '8px', height: '8px' }"></span>
              <span class="flex-grow-1">{{ cat.name }}</span>
              <span class="fw-semibold text-danger">-€{{ formatNum(cat.amount) }}</span>
            </div>
            <div v-if="!expenseCategories.some(c => c.amount > 0)" class="text-secondary fst-italic small">Keine Ausgaben</div>
          </div>
        </BCard>
      </BCol>

      <BCol lg="8">
        <BCard>
          <div class="d-flex justify-content-between align-items-center mb-3 flex-wrap gap-2">
            <span class="fw-bold">Monatsverlauf</span>
            <div class="d-flex gap-3 small text-secondary">
              <span><span class="d-inline-block rounded me-1" style="width:9px;height:9px;background:#10b981"></span> Einnahmen</span>
              <span><span class="d-inline-block rounded me-1" style="width:9px;height:9px;background:#ef4444"></span> Ausgaben</span>
            </div>
          </div>
          <canvas ref="barRef" width="480" height="240" class="w-100"></canvas>
        </BCard>
      </BCol>
    </BRow>

    <!-- Budget Limits -->
    <BCard v-if="expenseCategories.length > 0">
      <div class="d-flex justify-content-between align-items-center mb-3 flex-wrap gap-2">
        <span class="fw-bold">Budget-Limits</span>
        <div class="d-flex gap-2 align-items-center flex-wrap">
          <BBadge v-if="overBudgetCount > 0" variant="danger">{{ overBudgetCount }} überschritten</BBadge>
          <BButton size="sm" variant="outline-secondary" @click="openBudgetLimitModal">✏️ Limits anpassen</BButton>
        </div>
      </div>
      <div class="d-flex flex-column gap-3">
        <div v-for="cat in expenseCategories" :key="cat.id">
          <div class="d-flex justify-content-between align-items-center small mb-1">
            <span>{{ cat.icon }} {{ cat.name }}</span>
            <span>
              <strong>€{{ formatNum(cat.amount) }}</strong> / €{{ formatNum(cat.limit) }}
              <BBadge v-if="!cat.hasCustomLimit" variant="secondary" title="Kein eigenes Limit gesetzt — wird aus den Ausgaben abgeleitet.">auto</BBadge>
            </span>
          </div>
          <div class="progress" style="height: 7px;">
            <div
              class="progress-bar"
              role="progressbar"
              :style="{
                width: Math.min(100, (cat.amount / cat.limit) * 100) + '%',
                backgroundColor: cat.amount > cat.limit ? '#ef4444' : cat.color,
              }"
            ></div>
          </div>
          <div class="text-end small" :class="{ 'text-danger fw-semibold': cat.amount > cat.limit, 'text-secondary': cat.amount <= cat.limit }">
            {{ Math.round((cat.amount / cat.limit) * 100) }}%
          </div>
        </div>
      </div>
    </BCard>

    <!-- Transactions -->
    <BCard>
      <div class="d-flex justify-content-between align-items-center mb-3 flex-wrap gap-2">
        <span class="fw-bold">Transaktionen · {{ selectedMonthLabel }}</span>
        <div class="d-flex gap-2 align-items-center flex-wrap">
          <BButton v-if="recurringTransactions.length > 0" size="sm" variant="outline-primary" @click="showRecurringModal = true">
            🔁 Wiederkehrend verwalten
          </BButton>
          <BBadge variant="primary">{{ filteredTransactions.length }} Einträge</BBadge>
        </div>
      </div>

      <BForm class="mb-3" @submit.prevent="addTransaction">
        <BRow class="g-2 align-items-center">
          <BCol cols="auto">
            <BButtonGroup>
              <BButton size="sm" :variant="newTransaction.type === 'Expense' ? 'primary' : 'outline-primary'" type="button" title="Ausgabe" @click="newTransaction.type = 'Expense'">💸</BButton>
              <BButton size="sm" :variant="newTransaction.type === 'Income' ? 'primary' : 'outline-primary'" type="button" title="Einnahme" @click="newTransaction.type = 'Income'">💵</BButton>
            </BButtonGroup>
          </BCol>
          <BCol><BFormInput v-model="newTransaction.title" type="text" placeholder="Beschreibung, z. B. Supermarkt" required /></BCol>
          <BCol cols="auto"><BFormInput v-model.number="newTransaction.amount" type="number" step="0.01" min="0.01" placeholder="€ 0,00" required style="max-width: 120px;" /></BCol>
          <BCol cols="auto">
            <BFormSelect v-model="newTransaction.categoryId" required>
              <option value="" disabled>Kategorie</option>
              <option v-for="cat in filteredCategoriesForModal" :key="cat.id" :value="cat.id">{{ cat.name }}</option>
            </BFormSelect>
          </BCol>
          <BCol cols="auto"><BButton variant="primary" type="submit">＋ Hinzufügen</BButton></BCol>
        </BRow>
      </BForm>

      <div class="d-flex gap-2 mb-3 flex-wrap">
        <BFormInput v-model="txSearch" type="search" placeholder="🔍 Suchen (Titel, Notiz, Kategorie) …" class="flex-grow-1" style="min-width: 180px;" />
        <BFormSelect v-model="txTypeFilter" style="width: auto;">
          <option value="all">Alle Typen</option>
          <option value="Income">Einnahmen</option>
          <option value="Expense">Ausgaben</option>
        </BFormSelect>
      </div>

      <BTableSimple hover responsive class="align-middle">
        <thead>
          <tr>
            <th>Datum</th>
            <th>Beschreibung</th>
            <th>Kategorie</th>
            <th class="text-end">Betrag</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="tx in pagedTransactions" :key="tx.id">
            <td class="text-secondary">{{ formatDate(tx.date) }}</td>
            <td>
              <span class="fw-medium">{{ tx.title }}</span>
              <BBadge v-if="tx.isRecurring" variant="primary" class="ms-1" :title="`Wiederkehrend: ${intervalLabel(tx.recurringInterval)}`">
                {{ intervalIcon(tx.recurringInterval) }} {{ intervalLabel(tx.recurringInterval) }}
              </BBadge>
            </td>
            <td>
              <span class="badge rounded-pill" :style="{
                background: getCatColor(tx.categoryName, tx.categoryType) + '22',
                color: getCatColor(tx.categoryName, tx.categoryType),
              }">{{ tx.categoryName || '—' }}</span>
            </td>
            <td class="text-end fw-bold" :class="tx.categoryType === 'Income' ? 'text-success' : 'text-danger'">
              {{ tx.categoryType === 'Income' ? '+' : '-' }}€{{ Math.abs(tx.amount).toFixed(2) }}
            </td>
            <td class="text-end">
              <BButton v-if="tx.isRecurring" size="sm" variant="link" class="p-1 text-secondary" :disabled="stoppingRecurringId === tx.id" title="Wiederkehrend ausschalten" @click="stopRecurring(tx)">
                {{ stoppingRecurringId === tx.id ? '…' : '⏹' }}
              </BButton>
              <BButton size="sm" variant="link" class="p-1 text-secondary" title="Bearbeiten" @click="openEditModal(tx)">✏️</BButton>
              <BButton size="sm" variant="link" class="p-1 text-secondary" :disabled="deletingId === tx.id" title="Löschen" @click="deleteTransaction(tx.id)">
                {{ deletingId === tx.id ? '…' : '✕' }}
              </BButton>
            </td>
          </tr>
          <tr v-if="filteredTransactions.length === 0">
            <td colspan="5" class="text-center text-secondary py-4">Keine Transaktionen für diesen Monat/Filter.</td>
          </tr>
        </tbody>
      </BTableSimple>

      <div v-if="txTotalPages > 1" class="d-flex align-items-center justify-content-center gap-3 pt-2">
        <BButton size="sm" variant="outline-secondary" :disabled="txPage <= 1" @click="txPage--">‹ Zurück</BButton>
        <span class="small text-secondary">Seite {{ Math.min(txPage, txTotalPages) }} / {{ txTotalPages }}</span>
        <BButton size="sm" variant="outline-secondary" :disabled="txPage >= txTotalPages" @click="txPage++">Weiter ›</BButton>
      </div>
    </BCard>

    <!-- Management -->
    <BRow class="g-3">
      <BCol md="6">
        <BCard title="Kategorie anlegen">
          <BForm class="d-flex flex-column gap-3" @submit.prevent="createCategory">
            <div>
              <label class="form-label small fw-semibold">Name</label>
              <BFormInput v-model="newCategory.name" type="text" placeholder="z. B. Versicherungen" required />
            </div>
            <div>
              <label class="form-label small fw-semibold">Typ</label>
              <BFormSelect v-model="newCategory.type">
                <option value="Expense">Ausgabe</option>
                <option value="Income">Einnahme</option>
              </BFormSelect>
            </div>
            <div><BButton type="submit" variant="primary">Kategorie speichern</BButton></div>
          </BForm>
        </BCard>
      </BCol>

      <BCol md="6">
        <BCard>
          <div class="d-flex justify-content-between align-items-center mb-3">
            <span class="fw-bold">Kategorien</span>
            <BBadge variant="primary">{{ categories.length }} gesamt</BBadge>
          </div>
          <div class="d-flex flex-column gap-2">
            <div v-for="category in categories" :key="category.id" class="d-flex justify-content-between align-items-center border rounded p-2 bg-body-tertiary">
              <span class="text-truncate">{{ category.name }}</span>
              <div class="d-flex align-items-center gap-2">
                <BBadge :variant="category.type === 'Income' ? 'success' : 'danger'">
                  {{ category.type === 'Income' ? 'Einnahme' : 'Ausgabe' }}
                </BBadge>
                <BButton size="sm" variant="link" class="p-1 text-secondary" :disabled="deletingCategoryId === category.id" title="Kategorie löschen" @click="deleteCategory(category.id, category.name)">
                  {{ deletingCategoryId === category.id ? '…' : '✕' }}
                </BButton>
              </div>
            </div>
            <div v-if="categories.length === 0" class="text-center text-secondary py-3">Noch keine Kategorien.</div>
          </div>
        </BCard>
      </BCol>
    </BRow>

    <!-- ══════════════ MODALS ══════════════ -->

    <!-- Add Transaction -->
    <BModal v-model="showAddModal" title="Neue Transaktion" ok-title="Speichern" cancel-title="Abbrechen" @ok="addTransaction">
      <div class="d-flex flex-column gap-3">
        <BButtonGroup class="w-100">
          <BButton :variant="newTransaction.type === 'Expense' ? 'primary' : 'outline-primary'" @click="newTransaction.type = 'Expense'">💸 Ausgabe</BButton>
          <BButton :variant="newTransaction.type === 'Income' ? 'primary' : 'outline-primary'" @click="newTransaction.type = 'Income'">💵 Einnahme</BButton>
        </BButtonGroup>
        <div>
          <label class="form-label small fw-semibold">Beschreibung</label>
          <BFormInput v-model="newTransaction.title" placeholder="z. B. Supermarkt" />
        </div>
        <BRow class="g-2">
          <BCol>
            <label class="form-label small fw-semibold">Betrag (€)</label>
            <BFormInput v-model.number="newTransaction.amount" type="number" step="0.01" min="0" placeholder="0,00" />
          </BCol>
          <BCol>
            <label class="form-label small fw-semibold">Datum</label>
            <BFormInput v-model="newTransaction.date" type="date" />
          </BCol>
        </BRow>
        <div>
          <label class="form-label small fw-semibold">Kategorie</label>
          <BFormSelect v-model="newTransaction.categoryId">
            <option v-for="cat in filteredCategoriesForModal" :key="cat.id" :value="cat.id">{{ cat.name }}</option>
          </BFormSelect>
        </div>
        <div>
          <label class="form-label small fw-semibold">Notiz <span class="text-secondary fw-normal">optional</span></label>
          <BFormInput v-model="newTransaction.note" type="text" placeholder="Kurze Anmerkung …" />
        </div>
        <div class="border rounded p-3 bg-body-tertiary">
          <BFormCheckbox v-model="newTransaction.isRecurring" switch>🔁 Wiederkehrend</BFormCheckbox>
          <BFormRadioGroup
            v-if="newTransaction.isRecurring"
            v-model="newTransaction.recurringInterval"
            class="mt-2"
            buttons
            button-variant="outline-primary"
            size="sm"
            :options="[
              { text: 'Wöchentlich', value: 'weekly' },
              { text: 'Monatlich', value: 'monthly' },
              { text: 'Quartalsweise', value: 'quarterly' },
              { text: 'Jährlich', value: 'yearly' },
            ]"
          />
        </div>
      </div>
    </BModal>

    <!-- Edit Transaction -->
    <BModal
      v-model="showEditModal"
      :title="editingTemplate ? '🔁 Vorlage bearbeiten' : 'Transaktion bearbeiten'"
      :ok-title="savingEdit ? 'Speichert…' : 'Speichern'"
      cancel-title="Abbrechen"
      :ok-disabled="savingEdit"
      @ok.prevent="saveEdit"
    >
      <div v-if="editTransaction" class="d-flex flex-column gap-3">
        <p v-if="editingTemplate" class="text-secondary small mb-0">
          Änderungen gelten für alle künftigen Buchungen. Einen einzelnen Monat bearbeitest du direkt in der Transaktionsliste.
        </p>
        <BButtonGroup class="w-100">
          <BButton :variant="editTransaction.type === 'Expense' ? 'primary' : 'outline-primary'" @click="editTransaction.type = 'Expense'">💸 Ausgabe</BButton>
          <BButton :variant="editTransaction.type === 'Income' ? 'primary' : 'outline-primary'" @click="editTransaction.type = 'Income'">💵 Einnahme</BButton>
        </BButtonGroup>
        <div>
          <label class="form-label small fw-semibold">Beschreibung</label>
          <BFormInput v-model="editTransaction.title" />
        </div>
        <BRow class="g-2">
          <BCol>
            <label class="form-label small fw-semibold">Betrag (€)</label>
            <BFormInput v-model.number="editTransaction.amount" type="number" step="0.01" min="0" />
          </BCol>
          <BCol>
            <label class="form-label small fw-semibold">Datum</label>
            <BFormInput v-model="editTransaction.date" type="date" />
          </BCol>
        </BRow>
        <div>
          <label class="form-label small fw-semibold">Kategorie</label>
          <BFormSelect v-model="editTransaction.categoryId">
            <option v-for="cat in filteredCategoriesForEdit" :key="cat.id" :value="cat.id">{{ cat.name }}</option>
          </BFormSelect>
        </div>
        <div>
          <label class="form-label small fw-semibold">Notiz <span class="text-secondary fw-normal">optional</span></label>
          <BFormInput v-model="editTransaction.note" type="text" placeholder="Kurze Anmerkung …" />
        </div>
        <div class="border rounded p-3 bg-body-tertiary">
          <BFormCheckbox v-if="!editingTemplate" v-model="editTransaction.isRecurring" switch>🔁 Wiederkehrend</BFormCheckbox>
          <span v-else class="small fw-semibold">🔁 Intervall</span>
          <BFormRadioGroup
            v-if="editTransaction.isRecurring"
            v-model="editTransaction.recurringInterval"
            class="mt-2"
            buttons
            button-variant="outline-primary"
            size="sm"
            :options="[
              { text: 'Wöchentlich', value: 'weekly' },
              { text: 'Monatlich', value: 'monthly' },
              { text: 'Quartalsweise', value: 'quarterly' },
              { text: 'Jährlich', value: 'yearly' },
            ]"
          />
        </div>
      </div>
    </BModal>

    <!-- Recurring Overview -->
    <BModal v-model="showRecurringModal" title="🔁 Wiederkehrende Transaktionen" size="lg" ok-only ok-title="Schließen">
      <div v-if="recurringTransactions.length === 0" class="text-center text-secondary py-4">Keine wiederkehrenden Transaktionen.</div>
      <div v-else class="d-flex flex-column">
        <div v-for="tx in recurringTransactions" :key="'rm-' + tx.id" class="d-flex align-items-center justify-content-between gap-3 py-3 border-bottom flex-wrap">
          <div class="d-flex align-items-center gap-3">
            <span class="fs-4">{{ intervalIcon(tx.recurringInterval) }}</span>
            <div>
              <div class="fw-semibold">{{ tx.title }}</div>
              <div class="d-flex align-items-center gap-2 flex-wrap mt-1">
                <BBadge variant="primary">{{ intervalLabel(tx.recurringInterval) }}</BBadge>
                <span class="badge rounded-pill" :style="{
                  background: getCatColor(tx.categoryName, tx.categoryType) + '22',
                  color: getCatColor(tx.categoryName, tx.categoryType),
                }">{{ tx.categoryName }}</span>
                <span v-if="tx.nextDueDate" class="text-secondary small">Nächste: {{ formatDate(tx.nextDueDate) }}</span>
              </div>
            </div>
          </div>
          <div class="d-flex align-items-center gap-2">
            <span class="fw-bold" :class="tx.categoryType === 'Income' ? 'text-success' : 'text-danger'">
              {{ tx.categoryType === 'Income' ? '+' : '-' }}€{{ Math.abs(tx.amount).toFixed(2) }}
            </span>
            <BButton size="sm" variant="outline-secondary" @click="showRecurringModal = false; openEditModal(tx)">✏️ Bearbeiten</BButton>
            <BButton size="sm" variant="outline-warning" :disabled="stoppingRecurringId === tx.id" @click="stopRecurring(tx)">
              {{ stoppingRecurringId === tx.id ? '…' : '⏹ Stoppen' }}
            </BButton>
          </div>
        </div>
      </div>
    </BModal>

    <!-- Budget Limit -->
    <BModal v-model="showBudgetLimitModal" title="✏️ Budget-Limits anpassen">
      <p class="text-secondary small">Monatliches Limit pro Ausgaben-Kategorie. Leer lassen = automatisches Limit. Gilt für alle Geräte.</p>
      <div v-for="cat in expenseCategories" :key="'bl-' + cat.id" class="mb-3">
        <label class="form-label small fw-semibold">{{ cat.icon }} {{ cat.name }}</label>
        <BInputGroup prepend="€">
          <BFormInput v-model.number="editingLimits[cat.id]" type="number" min="0" step="10" placeholder="automatisch" />
        </BInputGroup>
      </div>
      <div v-if="expenseCategories.length === 0" class="text-center text-secondary py-3">Keine Ausgaben-Kategorien.</div>
      <template #footer>
        <BButton variant="outline-secondary" class="me-auto" @click="resetBudgetLimits">↺ Zurücksetzen</BButton>
        <BButton variant="outline-secondary" @click="showBudgetLimitModal = false">Abbrechen</BButton>
        <BButton variant="primary" :disabled="savingLimits" @click="saveBudgetLimits">{{ savingLimits ? 'Speichert…' : 'Speichern' }}</BButton>
      </template>
    </BModal>

  </BContainer>
</template>

<style scoped>
/* Nur das, was Bootstrap nicht abdeckt: Donut-Overlay + Bar-Canvas-Breite. */
.donut-wrapper {
  position: relative;
  display: flex;
  justify-content: center;
  margin-bottom: 14px;
}
.donut-center {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  text-align: center;
  pointer-events: none;
}
</style>
