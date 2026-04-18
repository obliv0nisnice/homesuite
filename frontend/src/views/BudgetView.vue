<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { apiFetch } from '@/services/api'

// ─── Types ────────────────────────────────────────────────────────────────────

type Category = {
  id: string
  name: string
  type: string
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
  recurringInterval?: 'monthly' | 'weekly' | 'yearly' | null
  nextDueDate?: string | null
}

type CategorySummary = {
  id: string
  name: string
  icon: string
  amount: number
  limit: number
  color: string
}

// ─── Constants ────────────────────────────────────────────────────────────────

const categoryPalette = [
  '#6366f1', '#10b981', '#f59e0b', '#ec4899', '#14b8a6', '#8b5cf6', '#ef4444', '#06b6d4',
]
const categoryIcons = ['🏠', '🛒', '🚗', '🎉', '💊', '📦', '🍽️', '📚']

// ─── State ────────────────────────────────────────────────────────────────────

const categories = ref<Category[]>([])
const transactions = ref<Transaction[]>([])
const loading = ref(false)
const error = ref('')
const success = ref('')
const showAddModal = ref(false)
const deletingId = ref<string | null>(null)
const stoppingRecurringId = ref<string | null>(null)
const deletingCategoryId = ref<string | null>(null)

const newCategory = ref({ name: '', type: 'Expense' })

const newTransaction = ref({
  title: '',
  type: 'Expense',
  amount: 0,
  date: new Date().toISOString().slice(0, 10),
  note: '',
  categoryId: '',
  isRecurring: false,
  recurringInterval: 'monthly' as 'monthly' | 'weekly' | 'yearly',
})

const donutRef = ref<HTMLCanvasElement | null>(null)
const barRef = ref<HTMLCanvasElement | null>(null)

// ─── Computed ─────────────────────────────────────────────────────────────────

const expenseCategories = computed<CategorySummary[]>(() =>
  categories.value
    .filter((c) => c.type === 'Expense')
    .map((category, index) => {
      const spent = transactions.value
        .filter((tx) => tx.categoryId === category.id && tx.categoryType === 'Expense')
        .reduce((sum, tx) => sum + Math.abs(tx.amount), 0)
      const derivedLimit = spent > 0 ? Math.ceil((spent * 1.15) / 10) * 10 : 100
      return {
        id: category.id,
        name: category.name,
        icon: categoryIcons[index % categoryIcons.length],
        amount: Number(spent.toFixed(2)),
        limit: derivedLimit,
        color: categoryPalette[index % categoryPalette.length],
      }
    }),
)

const incomeCategories = computed<CategorySummary[]>(() =>
  categories.value
    .filter((c) => c.type === 'Income')
    .map((category, index) => {
      const earned = transactions.value
        .filter((tx) => tx.categoryId === category.id && tx.categoryType === 'Income')
        .reduce((sum, tx) => sum + Math.abs(tx.amount), 0)
      return {
        id: category.id,
        name: category.name,
        icon: '💵',
        amount: Number(earned.toFixed(2)),
        limit: 0,
        color: ['#10b981', '#34d399', '#6ee7b7', '#059669'][index % 4],
      }
    }),
)

const recentTransactions = computed(() =>
  [...transactions.value]
    .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
    .slice(0, 10),
)

const recurringTransactions = computed(() =>
  transactions.value.filter((t) => t.isRecurring),
)

const totalIncome = computed(() =>
  transactions.value
    .filter((t) => t.categoryType === 'Income')
    .reduce((sum, t) => sum + Math.abs(t.amount), 0),
)

const totalExpenses = computed(() =>
  transactions.value
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

const currentMonth = computed(() =>
  new Date().toLocaleString('de-AT', { month: 'long', year: 'numeric' }),
)

const filteredCategoriesForModal = computed(() =>
  categories.value.filter((c) => c.type === newTransaction.value.type),
)

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatNum(n: number) {
  return n.toLocaleString('de-AT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString('de-AT')
}

function getCatColor(name: string) {
  return expenseCategories.value.find((c) => c.name === name)?.color ?? '#6366f1'
}

function isDarkMode() {
  return document.querySelector('.dark-mode') !== null
}

function intervalLabel(interval?: string | null) {
  if (interval === 'monthly') return 'Monatlich'
  if (interval === 'weekly') return 'Wöchentlich'
  if (interval === 'yearly') return 'Jährlich'
  return ''
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
        loadedCategories[0]?.id ??
        ''
    }
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
    await apiFetch<Category>('/categories', {
      method: 'POST',
      body: JSON.stringify(newCategory.value),
    })
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
        recurringInterval: newTransaction.value.isRecurring
          ? newTransaction.value.recurringInterval
          : null,
      }),
    })
    showAddModal.value = false
    newTransaction.value = {
      title: '',
      type: 'Expense',
      amount: 0,
      date: new Date().toISOString().slice(0, 10),
      note: '',
      categoryId:
        categories.value.find((c) => c.type === 'Expense')?.id ?? categories.value[0]?.id ?? '',
      isRecurring: false,
      recurringInterval: 'monthly',
    }
    success.value = 'Transaktion wurde angelegt.'
    await loadData()
  } catch {
    error.value = 'Transaktion konnte nicht erstellt werden.'
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
  if (!confirm(`Wiederholung für „${tx.title}" wirklich stoppen?`)) return
  stoppingRecurringId.value = tx.id
  error.value = ''
  try {
    await apiFetch(`/transactions/${tx.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        title: tx.title,
        amount: tx.amount,
        date: tx.date,
        note: tx.note,
        categoryId: tx.categoryId,
        isRecurring: false,
        recurringInterval: null,
      }),
    })
    // Update local state immediately
    const local = transactions.value.find((t) => t.id === tx.id)
    if (local) {
      local.isRecurring = false
      local.recurringInterval = null
    }
    success.value = `Wiederholung für „${tx.title}" gestoppt.`
  } catch {
    error.value = 'Wiederholung konnte nicht gestoppt werden.'
  } finally {
    stoppingRecurringId.value = null
  }
}

async function deleteCategory(id: string, name: string) {
  if (!confirm(`Kategorie „${name}" wirklich löschen?\nAchtung: Transaktionen dieser Kategorie bleiben erhalten.`)) return
  deletingCategoryId.value = id
  error.value = ''
  try {
    await apiFetch(`/categories/${id}`, { method: 'DELETE' })
    categories.value = categories.value.filter((c) => c.id !== id)
    success.value = `Kategorie „${name}" gelöscht.`
    await nextTick()
    renderCharts()
  } catch {
    error.value = 'Kategorie konnte nicht gelöscht werden. Möglicherweise sind noch Transaktionen verknüpft.'
  } finally {
    deletingCategoryId.value = null
  }
}

// ─── Charts ───────────────────────────────────────────────────────────────────

function renderDonut() {
  const canvas = donutRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const dark = isDarkMode()
  const W = 220
  const H = 220
  const cx = W / 2
  const cy = H / 2
  const outerR = 90
  const innerR = 58

  ctx.clearRect(0, 0, W, H)

  // Build chart data: all categories (income green, expense colored)
  const allData: { label: string; amount: number; color: string; type: string }[] = [
    ...incomeCategories.value
      .filter((c) => c.amount > 0)
      .map((c) => ({ label: c.name, amount: c.amount, color: c.color, type: 'Income' })),
    ...expenseCategories.value
      .filter((c) => c.amount > 0)
      .map((c) => ({ label: c.name, amount: c.amount, color: c.color, type: 'Expense' })),
  ]

  const total = allData.reduce((s, c) => s + c.amount, 0)

  if (total <= 0) {
    ctx.beginPath()
    ctx.arc(cx, cy, outerR, 0, Math.PI * 2)
    ctx.fillStyle = dark ? '#334155' : '#e2e8f0'
    ctx.fill()
    ctx.beginPath()
    ctx.arc(cx, cy, innerR, 0, Math.PI * 2)
    ctx.fillStyle = dark ? '#1e293b' : '#ffffff'
    ctx.fill()
    return
  }

  let angle = -Math.PI / 2
  allData.forEach((cat) => {
    const slice = (cat.amount / total) * Math.PI * 2
    ctx.beginPath()
    ctx.moveTo(cx, cy)
    ctx.arc(cx, cy, outerR, angle, angle + slice)
    ctx.closePath()
    ctx.fillStyle = cat.color
    ctx.shadowColor = `${cat.color}55`
    ctx.shadowBlur = 8
    ctx.fill()
    angle += slice
  })

  ctx.beginPath()
  ctx.arc(cx, cy, innerR, 0, Math.PI * 2)
  ctx.fillStyle = dark ? '#1e293b' : '#ffffff'
  ctx.shadowBlur = 0
  ctx.fill()
}

function renderBar() {
  const canvas = barRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const dark = isDarkMode()
  const W = canvas.offsetWidth || 480
  const H = 240
  canvas.width = W
  canvas.height = H
  ctx.clearRect(0, 0, W, H)

  const monthKeys = Array.from({ length: 6 }, (_, i) => {
    const d = new Date()
    d.setMonth(d.getMonth() - (5 - i))
    return {
      key: `${d.getFullYear()}-${d.getMonth()}`,
      label: d.toLocaleDateString('de-AT', { month: 'short' }),
    }
  })

  const income = monthKeys.map(({ key }) =>
    transactions.value
      .filter((tx) => {
        const d = new Date(tx.date)
        return `${d.getFullYear()}-${d.getMonth()}` === key && tx.categoryType === 'Income'
      })
      .reduce((sum, tx) => sum + Math.abs(tx.amount), 0),
  )

  const expenses = monthKeys.map(({ key }) =>
    transactions.value
      .filter((tx) => {
        const d = new Date(tx.date)
        return `${d.getFullYear()}-${d.getMonth()}` === key && tx.categoryType === 'Expense'
      })
      .reduce((sum, tx) => sum + Math.abs(tx.amount), 0),
  )

  const padL = 44
  const padR = 16
  const padT = 16
  const padB = 36
  const chartW = W - padL - padR
  const chartH = H - padT - padB
  const maxVal = Math.max(...income, ...expenses, 100) * 1.1
  const barW = (chartW / monthKeys.length) * 0.33
  const groupW = chartW / monthKeys.length

  for (let i = 0; i <= 4; i++) {
    const y = padT + chartH - (i / 4) * chartH
    ctx.beginPath()
    ctx.strokeStyle = dark ? '#2d3f5740' : '#e2e8f060'
    ctx.lineWidth = 1
    ctx.moveTo(padL, y)
    ctx.lineTo(W - padR, y)
    ctx.stroke()
    ctx.fillStyle = dark ? '#64748b' : '#94a3b8'
    ctx.font = '10px system-ui, sans-serif'
    ctx.textAlign = 'right'
    ctx.fillText('€' + Math.round((maxVal / 4) * i), padL - 6, y + 4)
  }

  monthKeys.forEach((month, i) => {
    const cx = padL + groupW * i + groupW / 2
    const incH = (income[i] / maxVal) * chartH
    const expH = (expenses[i] / maxVal) * chartH

    const grad1 = ctx.createLinearGradient(0, padT + chartH - incH, 0, padT + chartH)
    grad1.addColorStop(0, '#6366f1')
    grad1.addColorStop(1, '#818cf855')
    ctx.fillStyle = grad1
    roundRect(ctx, cx - barW - 2, padT + chartH - incH, barW, incH, 4)
    ctx.fill()

    const grad2 = ctx.createLinearGradient(0, padT + chartH - expH, 0, padT + chartH)
    grad2.addColorStop(0, '#ef4444')
    grad2.addColorStop(1, '#ef444455')
    ctx.fillStyle = grad2
    roundRect(ctx, cx + 2, padT + chartH - expH, barW, expH, 4)
    ctx.fill()

    ctx.fillStyle = dark ? '#94a3b8' : '#64748b'
    ctx.font = '11px system-ui, sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText(month.label, cx, H - 8)
  })
}

function roundRect(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number,
) {
  if (height <= 0) return
  ctx.beginPath()
  ctx.moveTo(x + radius, y)
  ctx.lineTo(x + width - radius, y)
  ctx.quadraticCurveTo(x + width, y, x + width, y + radius)
  ctx.lineTo(x + width, y + height)
  ctx.lineTo(x, y + height)
  ctx.lineTo(x, y + radius)
  ctx.quadraticCurveTo(x, y, x + radius, y)
  ctx.closePath()
}

function renderCharts() {
  renderDonut()
  renderBar()
}

// ─── Lifecycle ────────────────────────────────────────────────────────────────

onMounted(async () => {
  await loadData()
  const observer = new MutationObserver(() => nextTick(() => renderCharts()))
  observer.observe(document.querySelector('.app-wrapper') || document.body, {
    attributes: true,
    attributeFilter: ['class'],
  })
})

watch(
  () => newTransaction.value.type,
  (type) => {
    const match = categories.value.find((c) => c.type === type)
    if (match) newTransaction.value.categoryId = match.id
  },
)

watch([transactions, categories], async () => {
  await nextTick()
  renderCharts()
}, { deep: true })
</script>

<template>
  <div class="budget-page">

    <!-- ── Header ── -->
    <div class="page-header">
      <div>
        <h1 class="page-title">Budget <span class="title-accent">Übersicht</span></h1>
        <p class="page-subtitle">{{ currentMonth }} · Finanzen auf einen Blick</p>
      </div>
      <div class="page-actions">
        <button class="btn-secondary" @click="loadData" :disabled="loading">
          <span>↺</span> Neu laden
        </button>
        <button class="btn-add" @click="showAddModal = true">
          <span>＋</span> Transaktion
        </button>
      </div>
    </div>

    <!-- ── Alerts ── -->
    <div v-if="error" class="alert alert-error">⚠ {{ error }}</div>
    <div v-if="success" class="alert alert-success">✓ {{ success }}</div>

    <!-- ── KPI Cards ── -->
    <div class="stats-grid">
      <div class="stat-card stat-income">
        <div class="stat-icon">💵</div>
        <div class="stat-info">
          <span class="stat-label">Einnahmen</span>
          <span class="stat-value">€ {{ formatNum(totalIncome) }}</span>
          <span class="stat-trend trend-up">↑ diesen Monat</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
      <div class="stat-card stat-expenses">
        <div class="stat-icon">💸</div>
        <div class="stat-info">
          <span class="stat-label">Ausgaben</span>
          <span class="stat-value">€ {{ formatNum(totalExpenses) }}</span>
          <span class="stat-trend trend-down">{{ expensePercent }}% der Einnahmen</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
      <div class="stat-card stat-balance">
        <div class="stat-icon">🏦</div>
        <div class="stat-info">
          <span class="stat-label">Restbudget</span>
          <span class="stat-value">€ {{ formatNum(balance) }}</span>
          <span class="stat-trend" :class="balance >= 0 ? 'trend-up' : 'trend-down'">
            {{ balance >= 0 ? '✓ Im grünen Bereich' : '⚠ Überzogen' }}
          </span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
      <div class="stat-card stat-savings">
        <div class="stat-icon">🎯</div>
        <div class="stat-info">
          <span class="stat-label">Sparquote</span>
          <span class="stat-value">{{ savingsRate }}%</span>
          <span class="stat-trend trend-up">Ziel: 20%</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
    </div>

    <!-- ── Charts Row ── -->
    <div class="charts-row">
      <!-- Donut: Income + Expenses by category -->
      <div class="chart-card chart-donut-card">
        <div class="chart-header">
          <span class="chart-title">Verteilung</span>
          <span class="chart-badge">{{ currentMonth }}</span>
        </div>
        <div class="donut-wrapper">
          <canvas ref="donutRef" width="220" height="220"></canvas>
          <div class="donut-center">
            <span class="donut-total">€{{ formatNum(balance) }}</span>
            <span class="donut-label">Bilanz</span>
          </div>
        </div>
        <div class="legend">
          <div class="legend-section-title">Einnahmen</div>
          <div v-for="cat in incomeCategories.filter(c => c.amount > 0)" :key="'i-' + cat.id" class="legend-item">
            <span class="legend-dot" :style="{ background: cat.color }"></span>
            <span class="legend-name">{{ cat.name }}</span>
            <span class="legend-val amount-pos">+€{{ formatNum(cat.amount) }}</span>
          </div>
          <div v-if="incomeCategories.filter(c => c.amount > 0).length === 0" class="legend-empty">Keine Einnahmen</div>

          <div class="legend-section-title" style="margin-top: 10px">Ausgaben</div>
          <div v-for="cat in expenseCategories.filter(c => c.amount > 0)" :key="'e-' + cat.id" class="legend-item">
            <span class="legend-dot" :style="{ background: cat.color }"></span>
            <span class="legend-name">{{ cat.name }}</span>
            <span class="legend-val amount-neg">-€{{ formatNum(cat.amount) }}</span>
          </div>
          <div v-if="expenseCategories.filter(c => c.amount > 0).length === 0" class="legend-empty">Keine Ausgaben</div>
        </div>
      </div>

      <!-- Bar chart -->
      <div class="chart-card chart-bar-card">
        <div class="chart-header">
          <span class="chart-title">Monatsverlauf</span>
          <div class="chart-legend-inline">
            <span><span class="dot dot-income"></span> Einnahmen</span>
            <span><span class="dot dot-expense"></span> Ausgaben</span>
          </div>
        </div>
        <canvas ref="barRef" width="480" height="240"></canvas>
      </div>
    </div>

    <!-- ── Budget Limits ── -->
    <div class="budget-limits-card" v-if="expenseCategories.length > 0">
      <div class="chart-header">
        <span class="chart-title">Budget-Limits</span>
        <span class="chart-badge warning" v-if="overBudgetCount > 0">
          {{ overBudgetCount }} überschritten
        </span>
      </div>
      <div class="progress-list">
        <div v-for="cat in expenseCategories" :key="cat.id" class="progress-item">
          <div class="progress-header">
            <span class="progress-name">{{ cat.icon }} {{ cat.name }}</span>
            <span class="progress-nums">
              <strong>€{{ formatNum(cat.amount) }}</strong> / €{{ formatNum(cat.limit) }}
            </span>
          </div>
          <div class="progress-track">
            <div
              class="progress-fill"
              :style="{
                width: Math.min(100, (cat.amount / cat.limit) * 100) + '%',
                background: cat.amount > cat.limit ? '#ef4444' : cat.color,
              }"
            ></div>
          </div>
          <span class="progress-pct" :class="{ danger: cat.amount > cat.limit }">
            {{ Math.round((cat.amount / cat.limit) * 100) }}%
          </span>
        </div>
      </div>
    </div>

    <!-- ── Transactions Table ── -->
    <div class="transactions-card">
      <div class="chart-header">
        <span class="chart-title">Letzte Transaktionen</span>
        <span class="chart-badge">{{ recentTransactions.length }} Einträge</span>
      </div>
      <div class="trans-table">
        <div class="trans-row header-row">
          <span>Datum</span>
          <span>Beschreibung</span>
          <span>Kategorie</span>
          <span class="text-right">Betrag</span>
          <span></span>
        </div>
        <div v-for="tx in recentTransactions" :key="tx.id" class="trans-row">
          <span class="trans-date">{{ formatDate(tx.date) }}</span>
          <span class="trans-desc">
            {{ tx.title }}
            <span v-if="tx.isRecurring" class="recurring-badge" :title="intervalLabel(tx.recurringInterval)">
              🔁 {{ intervalLabel(tx.recurringInterval) }}
            </span>
          </span>
          <span class="trans-cat">
            <span
              class="cat-chip"
              :style="{
                background: getCatColor(tx.categoryName) + '22',
                color: getCatColor(tx.categoryName),
              }"
            >
              {{ tx.categoryName || '—' }}
            </span>
          </span>
          <span class="trans-amount" :class="tx.categoryType === 'Income' ? 'amount-pos' : 'amount-neg'">
            {{ tx.categoryType === 'Income' ? '+' : '-' }}€{{ Math.abs(tx.amount).toFixed(2) }}
          </span>
          <span class="trans-actions">
            <button
              class="btn-delete"
              @click="deleteTransaction(tx.id)"
              :disabled="deletingId === tx.id"
              title="Löschen"
            >
              {{ deletingId === tx.id ? '…' : '✕' }}
            </button>
          </span>
        </div>
        <div v-if="recentTransactions.length === 0" class="empty-state">
          Noch keine Transaktionen vorhanden.
        </div>
      </div>
    </div>

    <!-- ── Recurring Transactions ── -->
    <div class="transactions-card" v-if="recurringTransactions.length > 0">
      <div class="chart-header">
        <span class="chart-title">🔁 Wiederkehrende Transaktionen</span>
        <span class="chart-badge">{{ recurringTransactions.length }} aktiv</span>
      </div>
      <div class="trans-table">
        <div class="trans-row recurring-header-row">
          <span>Beschreibung</span>
          <span>Kategorie</span>
          <span>Intervall</span>
          <span>Nächste Ausführung</span>
          <span class="text-right">Betrag</span>
          <span></span>
        </div>
        <div v-for="tx in recurringTransactions" :key="'r-' + tx.id" class="trans-row recurring-row">
          <span class="trans-desc">{{ tx.title }}</span>
          <span class="trans-cat">
            <span
              class="cat-chip"
              :style="{
                background: getCatColor(tx.categoryName) + '22',
                color: getCatColor(tx.categoryName),
              }"
            >{{ tx.categoryName || '—' }}</span>
          </span>
          <span class="interval-chip">{{ intervalLabel(tx.recurringInterval) }}</span>
          <span class="next-due-date">
            {{ tx.nextDueDate ? formatDate(tx.nextDueDate) : '—' }}
          </span>
          <span class="trans-amount" :class="tx.categoryType === 'Income' ? 'amount-pos' : 'amount-neg'">
            {{ tx.categoryType === 'Income' ? '+' : '-' }}€{{ Math.abs(tx.amount).toFixed(2) }}
          </span>
          <span class="trans-actions">
            <button
              class="btn-stop-recurring"
              @click="stopRecurring(tx)"
              :disabled="stoppingRecurringId === tx.id"
              title="Wiederholung stoppen"
            >{{ stoppingRecurringId === tx.id ? '…' : '⏹' }}</button>
            <button
              class="btn-delete"
              @click="deleteTransaction(tx.id)"
              :disabled="deletingId === tx.id"
              title="Löschen"
            >{{ deletingId === tx.id ? '…' : '✕' }}</button>
          </span>
        </div>
      </div>
    </div>

    <!-- ── Management ── -->
    <div class="management-grid">
      <div class="management-card">
        <div class="chart-header">
          <span class="chart-title">Kategorie anlegen</span>
        </div>
        <form class="management-form" @submit.prevent="createCategory">
          <div class="form-group">
            <label>Name</label>
            <input v-model="newCategory.name" class="form-input" type="text" placeholder="z. B. Versicherungen" required />
          </div>
          <div class="form-group">
            <label>Typ</label>
            <select v-model="newCategory.type" class="form-input">
              <option value="Expense">Ausgabe</option>
              <option value="Income">Einnahme</option>
            </select>
          </div>
          <button class="btn-save" type="submit">Kategorie speichern</button>
        </form>
      </div>

      <div class="management-card">
        <div class="chart-header">
          <span class="chart-title">Kategorien</span>
          <span class="chart-badge">{{ categories.length }} gesamt</span>
        </div>
        <div class="category-list">
          <div v-for="category in categories" :key="category.id" class="category-row">
            <span class="category-name">{{ category.name }}</span>
            <div class="category-row-actions">
              <span class="cat-chip small-chip" :class="category.type === 'Income' ? 'chip-income' : 'chip-expense'">
                {{ category.type === 'Income' ? 'Einnahme' : 'Ausgabe' }}
              </span>
              <button
                class="btn-delete"
                @click="deleteCategory(category.id, category.name)"
                :disabled="deletingCategoryId === category.id"
                title="Kategorie löschen"
              >{{ deletingCategoryId === category.id ? '…' : '✕' }}</button>
            </div>
          </div>
          <div v-if="categories.length === 0" class="empty-state">Noch keine Kategorien.</div>
        </div>
      </div>
    </div>

    <!-- ── Add Transaction Modal ── -->
    <Teleport to="body">
      <div v-if="showAddModal" class="modal-backdrop" @click.self="showAddModal = false">
        <div class="modal-box">
          <div class="modal-header">
            <h2>Neue Transaktion</h2>
            <button class="modal-close" @click="showAddModal = false">✕</button>
          </div>

          <div class="modal-body">
            <!-- Typ toggle -->
            <div class="type-toggle">
              <button
                class="type-btn"
                :class="{ active: newTransaction.type === 'Expense' }"
                @click="newTransaction.type = 'Expense'"
              >💸 Ausgabe</button>
              <button
                class="type-btn"
                :class="{ active: newTransaction.type === 'Income' }"
                @click="newTransaction.type = 'Income'"
              >💵 Einnahme</button>
            </div>

            <!-- Beschreibung -->
            <div class="form-group">
              <label>Beschreibung</label>
              <input v-model="newTransaction.title" class="form-input" placeholder="z. B. Supermarkt" />
            </div>

            <!-- Betrag + Datum (2 cols) -->
            <div class="form-row-2">
              <div class="form-group">
                <label>Betrag (€)</label>
                <input
                  v-model.number="newTransaction.amount"
                  class="form-input"
                  type="number"
                  step="0.01"
                  min="0"
                  placeholder="0,00"
                />
              </div>
              <div class="form-group">
                <label>Datum</label>
                <input v-model="newTransaction.date" class="form-input" type="date" />
              </div>
            </div>

            <!-- Kategorie -->
            <div class="form-group">
              <label>Kategorie</label>
              <select v-model="newTransaction.categoryId" class="form-input">
                <option v-for="cat in filteredCategoriesForModal" :key="cat.id" :value="cat.id">
                  {{ cat.name }}
                </option>
              </select>
            </div>

            <!-- Notiz -->
            <div class="form-group">
              <label>Notiz <span class="optional">optional</span></label>
              <input v-model="newTransaction.note" class="form-input" type="text" placeholder="Kurze Anmerkung …" />
            </div>

            <!-- Wiederkehrend -->
            <div class="recurring-section">
              <label class="recurring-toggle">
                <span class="toggle-label">🔁 Wiederkehrend</span>
                <div class="toggle-switch">
                  <input type="checkbox" v-model="newTransaction.isRecurring" />
                  <span class="toggle-slider"></span>
                </div>
              </label>
              <div v-if="newTransaction.isRecurring" class="form-group" style="margin-top: 12px">
                <label>Intervall</label>
                <div class="interval-options">
                  <label class="interval-opt" :class="{ active: newTransaction.recurringInterval === 'weekly' }">
                    <input type="radio" v-model="newTransaction.recurringInterval" value="weekly" />
                    Wöchentlich
                  </label>
                  <label class="interval-opt" :class="{ active: newTransaction.recurringInterval === 'monthly' }">
                    <input type="radio" v-model="newTransaction.recurringInterval" value="monthly" />
                    Monatlich
                  </label>
                  <label class="interval-opt" :class="{ active: newTransaction.recurringInterval === 'yearly' }">
                    <input type="radio" v-model="newTransaction.recurringInterval" value="yearly" />
                    Jährlich
                  </label>
                </div>
              </div>
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn-cancel" @click="showAddModal = false">Abbrechen</button>
            <button class="btn-save" @click="addTransaction">Speichern</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
/* ── Variables & Base ───────────────────────────────────────────────────────── */
.budget-page {
  max-width: 1200px;
  margin: 0 auto;
  padding: 32px 24px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

/* ── Header ─────────────────────────────────────────────────────────────────── */
.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 16px;
}

.page-actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.page-title {
  font-size: 30px;
  font-weight: 800;
  color: var(--text);
  letter-spacing: -1px;
}

.title-accent { color: var(--primary); }

.page-subtitle {
  color: var(--text-muted);
  font-size: 13px;
  margin-top: 4px;
}

/* ── Alerts ─────────────────────────────────────────────────────────────────── */
.alert {
  padding: 12px 16px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
}

.alert-error {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
  border: 1px solid rgba(239, 68, 68, 0.2);
}

.alert-success {
  background: rgba(16, 185, 129, 0.1);
  color: #10b981;
  border: 1px solid rgba(16, 185, 129, 0.2);
}

/* ── Buttons ────────────────────────────────────────────────────────────────── */
.btn-add,
.btn-save,
.btn-secondary {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 10px 20px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.15s, transform 0.1s;
  white-space: nowrap;
}

.btn-add,
.btn-save {
  background: var(--primary);
  color: white;
  border: none;
}

.btn-secondary {
  background: var(--surface);
  color: var(--text);
  border: 1px solid var(--border);
}

.btn-add:hover,
.btn-save:hover,
.btn-secondary:hover {
  opacity: 0.88;
  transform: translateY(-1px);
}

.btn-delete {
  background: none;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  font-size: 13px;
  padding: 4px 8px;
  border-radius: 6px;
  transition: background 0.15s, color 0.15s;
  line-height: 1;
}

.btn-delete:hover {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.btn-delete:disabled {
  opacity: 0.5;
  cursor: default;
}

/* ── Stat Cards ─────────────────────────────────────────────────────────────── */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 14px;
}

@media (max-width: 900px) {
  .stats-grid { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 540px) {
  .stats-grid { grid-template-columns: 1fr; }
}

.stat-card {
  position: relative;
  overflow: hidden;
  background: var(--surface);
  border-radius: var(--radius);
  padding: 20px 18px;
  display: flex;
  align-items: center;
  gap: 14px;
  box-shadow: var(--card-shadow);
  border: 1px solid var(--border);
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.stat-card:hover { transform: translateY(-2px); }

.stat-icon { font-size: 30px; z-index: 1; }

.stat-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  z-index: 1;
  min-width: 0;
}

.stat-label {
  font-size: 11px;
  color: var(--text-muted);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.stat-value {
  font-size: 20px;
  font-weight: 800;
  color: var(--text);
  letter-spacing: -0.5px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.stat-trend { font-size: 11px; font-weight: 500; }
.trend-up { color: #10b981; }
.trend-down { color: #ef4444; }

.stat-bg-shape {
  position: absolute;
  right: -20px;
  top: -20px;
  width: 90px;
  height: 90px;
  border-radius: 50%;
  opacity: 0.07;
}

.stat-income .stat-bg-shape { background: #10b981; }
.stat-expenses .stat-bg-shape { background: #ef4444; }
.stat-balance .stat-bg-shape { background: #6366f1; }
.stat-savings .stat-bg-shape { background: #f59e0b; }

/* ── Charts Row ─────────────────────────────────────────────────────────────── */
.charts-row {
  display: grid;
  grid-template-columns: 300px 1fr;
  gap: 16px;
}

@media (max-width: 780px) {
  .charts-row { grid-template-columns: 1fr; }
}

.chart-card,
.budget-limits-card,
.transactions-card,
.management-card {
  background: var(--surface);
  border-radius: var(--radius);
  padding: 22px;
  box-shadow: var(--card-shadow);
  border: 1px solid var(--border);
}

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 18px;
  flex-wrap: wrap;
  gap: 8px;
}

.chart-title {
  font-size: 15px;
  font-weight: 700;
  color: var(--text);
}

.chart-badge {
  font-size: 12px;
  padding: 3px 10px;
  background: rgba(99, 102, 241, 0.1);
  color: var(--primary);
  border-radius: 20px;
  font-weight: 600;
}

.chart-badge.warning {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

/* ── Donut ───────────────────────────────────────────────────────────────────── */
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

.donut-total {
  display: block;
  font-size: 15px;
  font-weight: 800;
  color: var(--text);
}

.donut-label {
  font-size: 11px;
  color: var(--text-muted);
}

/* ── Legend ─────────────────────────────────────────────────────────────────── */
.legend {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 200px;
  overflow-y: auto;
}

.legend-section-title {
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--text-muted);
  margin-top: 4px;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
}

.legend-dot {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  flex-shrink: 0;
}

.legend-name { flex: 1; color: var(--text); }

.legend-val { font-weight: 600; }

.legend-empty {
  font-size: 12px;
  color: var(--text-muted);
  font-style: italic;
}

.chart-legend-inline {
  display: flex;
  gap: 14px;
  font-size: 12px;
  color: var(--text-muted);
}

.dot {
  display: inline-block;
  width: 9px;
  height: 9px;
  border-radius: 2px;
  margin-right: 4px;
}

.dot-income { background: #6366f1; }
.dot-expense { background: #ef4444; }
.chart-bar-card canvas { width: 100%; }

/* ── Budget Limits ──────────────────────────────────────────────────────────── */
.progress-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.progress-item {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.progress-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.progress-name,
.progress-nums { font-size: 13px; color: var(--text); }

.progress-track {
  height: 7px;
  background: var(--border);
  border-radius: 99px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  border-radius: 99px;
  transition: width 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

.progress-pct {
  font-size: 11px;
  color: var(--text-muted);
  align-self: flex-end;
}

.progress-pct.danger {
  color: #ef4444;
  font-weight: 600;
}

/* ── Transactions Table ─────────────────────────────────────────────────────── */
.trans-table { display: flex; flex-direction: column; }

.trans-row {
  display: grid;
  grid-template-columns: 100px 1fr 130px 110px 36px;
  padding: 11px 8px;
  border-bottom: 1px solid var(--border);
  align-items: center;
  font-size: 13px;
  gap: 4px;
}

.trans-row:last-child { border-bottom: none; }

.trans-row:hover:not(.header-row) {
  background: var(--surface2);
  border-radius: 8px;
}

.header-row {
  color: var(--text-muted);
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 2px solid var(--border);
}

.trans-date { color: var(--text-muted); }
.trans-desc { color: var(--text); font-weight: 500; display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.text-right { text-align: right; }
.trans-amount { text-align: right; font-weight: 700; }
.trans-actions { display: flex; justify-content: flex-end; }

.amount-pos { color: #10b981; }
.amount-neg { color: #ef4444; }

.cat-chip {
  display: inline-block;
  padding: 2px 9px;
  border-radius: 20px;
  font-size: 11px;
  font-weight: 600;
}

.small-chip {
  background: var(--surface2);
  color: var(--text-muted);
}

.chip-income {
  background: rgba(16, 185, 129, 0.12);
  color: #10b981;
}

.chip-expense {
  background: rgba(239, 68, 68, 0.12);
  color: #ef4444;
}

.recurring-badge {
  font-size: 11px;
  color: var(--primary);
  background: rgba(99, 102, 241, 0.1);
  padding: 1px 7px;
  border-radius: 20px;
  font-weight: 500;
}

.interval-chip {
  font-size: 12px;
  color: var(--primary);
  font-weight: 600;
}

.next-due-date {
  font-size: 12px;
  color: var(--text-muted);
}

.recurring-header-row {
  grid-template-columns: 1fr 120px 100px 120px 100px 72px !important;
}

.recurring-row {
  grid-template-columns: 1fr 120px 100px 120px 100px 72px !important;
}

.btn-stop-recurring {
  background: none;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  font-size: 13px;
  padding: 4px 6px;
  border-radius: 6px;
  transition: background 0.15s, color 0.15s;
  line-height: 1;
}

.btn-stop-recurring:hover {
  background: rgba(245, 158, 11, 0.12);
  color: #f59e0b;
}

.btn-stop-recurring:disabled {
  opacity: 0.5;
  cursor: default;
}

.category-row-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.category-name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.empty-state {
  padding: 24px;
  text-align: center;
  color: var(--text-muted);
  font-size: 14px;
}

/* ── Management ─────────────────────────────────────────────────────────────── */
.management-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

@media (max-width: 900px) {
  .management-grid { grid-template-columns: 1fr; }
}

.management-form,
.category-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.category-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 13px;
  border: 1px solid var(--border);
  border-radius: 10px;
  background: var(--surface2);
  font-size: 13px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.form-group label {
  font-size: 12px;
  font-weight: 600;
  color: var(--text);
}

.optional {
  font-weight: 400;
  color: var(--text-muted);
  font-size: 11px;
  margin-left: 4px;
}

.form-input {
  padding: 9px 13px;
  border-radius: 9px;
  border: 1.5px solid var(--border);
  background: var(--surface2);
  color: var(--text);
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s;
  width: 100%;
  box-sizing: border-box;
}

.form-input:focus { border-color: var(--primary); }

/* ── Modal ───────────────────────────────────────────────────────────────────── */
.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 999;
  backdrop-filter: blur(4px);
  padding: 16px;
}

.modal-box {
  background: var(--surface);
  border-radius: var(--radius);
  width: 100%;
  max-width: 460px;
  max-height: 92vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  border: 1px solid var(--border);
  animation: slideUp 0.22s ease;
}

@keyframes slideUp {
  from { opacity: 0; transform: translateY(16px); }
  to { opacity: 1; transform: translateY(0); }
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 18px 22px;
  border-bottom: 1px solid var(--border);
  position: sticky;
  top: 0;
  background: var(--surface);
  z-index: 1;
}

.modal-header h2 {
  font-size: 17px;
  font-weight: 700;
  color: var(--text);
}

.modal-close {
  background: none;
  border: none;
  font-size: 16px;
  cursor: pointer;
  color: var(--text-muted);
  padding: 4px 8px;
  border-radius: 6px;
  transition: background 0.15s;
}

.modal-close:hover { background: var(--surface2); }

.modal-body {
  padding: 20px 22px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.modal-footer {
  display: flex;
  gap: 10px;
  justify-content: flex-end;
  padding: 14px 22px;
  border-top: 1px solid var(--border);
  position: sticky;
  bottom: 0;
  background: var(--surface);
}

.btn-cancel {
  padding: 9px 18px;
  background: none;
  border: 1.5px solid var(--border);
  color: var(--text);
  border-radius: 9px;
  cursor: pointer;
  font-weight: 600;
  font-size: 14px;
  transition: background 0.15s;
}

.btn-cancel:hover { background: var(--surface2); }

/* ── Type Toggle ─────────────────────────────────────────────────────────────── */
.type-toggle {
  display: flex;
  gap: 8px;
  background: var(--surface2);
  border-radius: 10px;
  padding: 4px;
}

.type-btn {
  flex: 1;
  padding: 8px;
  border: none;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  background: transparent;
  color: var(--text-muted);
  transition: background 0.15s, color 0.15s;
}

.type-btn.active {
  background: var(--surface);
  color: var(--text);
  box-shadow: 0 1px 4px rgba(0,0,0,0.12);
}

/* ── 2-col form row ─────────────────────────────────────────────────────────── */
.form-row-2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

/* ── Recurring section ──────────────────────────────────────────────────────── */
.recurring-section {
  border: 1.5px solid var(--border);
  border-radius: 10px;
  padding: 12px 14px;
  background: var(--surface2);
}

.recurring-toggle {
  display: flex;
  align-items: center;
  justify-content: space-between;
  cursor: pointer;
}

.toggle-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--text);
}

.toggle-switch {
  position: relative;
  width: 40px;
  height: 22px;
}

.toggle-switch input {
  opacity: 0;
  width: 0;
  height: 0;
}

.toggle-slider {
  position: absolute;
  inset: 0;
  background: var(--border);
  border-radius: 22px;
  transition: background 0.2s;
  cursor: pointer;
}

.toggle-slider::before {
  content: '';
  position: absolute;
  left: 3px;
  top: 3px;
  width: 16px;
  height: 16px;
  background: white;
  border-radius: 50%;
  transition: transform 0.2s;
}

.toggle-switch input:checked + .toggle-slider {
  background: var(--primary);
}

.toggle-switch input:checked + .toggle-slider::before {
  transform: translateX(18px);
}

.interval-options {
  display: flex;
  gap: 8px;
}

.interval-opt {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  padding: 7px 4px;
  border: 1.5px solid var(--border);
  border-radius: 8px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  background: var(--surface);
  color: var(--text-muted);
  transition: border-color 0.15s, color 0.15s, background 0.15s;
  text-align: center;
}

.interval-opt input { display: none; }

.interval-opt.active {
  border-color: var(--primary);
  color: var(--primary);
  background: rgba(99, 102, 241, 0.08);
}

/* ── Responsive ─────────────────────────────────────────────────────────────── */
@media (max-width: 760px) {
  .budget-page { padding: 16px 12px; }

  .trans-row {
    grid-template-columns: 90px 1fr 80px 36px;
  }

  .trans-row .trans-cat { display: none; }

  .form-row-2 { grid-template-columns: 1fr; }

  .interval-options { flex-direction: column; }
}
</style>
