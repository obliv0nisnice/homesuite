<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { apiFetch } from '@/services/api'

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
}

type CategorySummary = {
  id: string
  name: string
  icon: string
  amount: number
  limit: number
  color: string
}

const categoryPalette = [
  '#6366f1', '#10b981', '#f59e0b', '#ec4899', '#14b8a6', '#8b5cf6', '#ef4444', '#06b6d4',
]
const categoryIcons = ['🏠', '🛒', '🚗', '🎉', '💊', '📦', '🍽️', '📚']

const categories = ref<Category[]>([])
const transactions = ref<Transaction[]>([])
const loading = ref(false)
const error = ref('')
const success = ref('')
const showAddModal = ref(false)

const newCategory = ref({
  name: '',
  type: 'Expense',
})

const newTransaction = ref({
  title: '',
  amount: 0,
  date: new Date().toISOString().slice(0, 16),
  note: '',
  categoryId: '',
})

const donutRef = ref<HTMLCanvasElement | null>(null)
const barRef = ref<HTMLCanvasElement | null>(null)

const expenseCategories = computed<CategorySummary[]>(() => {
  const expenseOnly = categories.value.filter((c) => c.type === 'Expense')

  return expenseOnly.map((category, index) => {
    const spent = transactions.value
      .filter((tx) => tx.categoryId === category.id && tx.categoryType === 'Expense')
      .reduce((sum, tx) => sum + Math.abs(tx.amount), 0)

    const derivedLimit = spent > 0 ? Math.ceil(spent * 1.15 / 10) * 10 : 100

    return {
      id: category.id,
      name: category.name,
      icon: categoryIcons[index % categoryIcons.length],
      amount: Number(spent.toFixed(2)),
      limit: derivedLimit,
      color: categoryPalette[index % categoryPalette.length],
    }
  })
})

const recentTransactions = computed(() =>
  [...transactions.value]
    .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
    .slice(0, 8),
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
  totalIncome.value > 0 ? Math.max(0, Math.round(((totalIncome.value - totalExpenses.value) / totalIncome.value) * 100)) : 0,
)
const overBudgetCount = computed(() => expenseCategories.value.filter((c) => c.amount > c.limit).length)
const currentMonth = computed(() =>
  new Date().toLocaleString('de-AT', { month: 'long', year: 'numeric' }),
)

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
      newTransaction.value.categoryId = loadedCategories.find((c) => c.type === 'Expense')?.id ?? loadedCategories[0]?.id ?? ''
    }

    await nextTick()
    renderCharts()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Daten konnten nicht geladen werden.'
  } finally {
    loading.value = false
  }
}

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
  } catch (err) {
    console.error(err)
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
        ...newTransaction.value,
        amount: Number(newTransaction.value.amount),
        date: new Date(newTransaction.value.date).toISOString(),
      }),
    })

    showAddModal.value = false
    newTransaction.value = {
      title: '',
      amount: 0,
      date: new Date().toISOString().slice(0, 16),
      note: '',
      categoryId: categories.value.find((c) => c.type === 'Expense')?.id ?? categories.value[0]?.id ?? '',
    }

    success.value = 'Transaktion wurde angelegt.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = 'Transaktion konnte nicht erstellt werden.'
  }
}

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

  const chartData = expenseCategories.value.filter((c) => c.amount > 0)
  const total = chartData.reduce((s, c) => s + c.amount, 0)

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

  chartData.forEach((cat) => {
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

  const income = monthKeys.map(({ key }) => {
    return transactions.value
      .filter((tx) => {
        const d = new Date(tx.date)
        return `${d.getFullYear()}-${d.getMonth()}` === key && tx.categoryType === 'Income'
      })
      .reduce((sum, tx) => sum + Math.abs(tx.amount), 0)
  })

  const expenses = monthKeys.map(({ key }) => {
    return transactions.value
      .filter((tx) => {
        const d = new Date(tx.date)
        return `${d.getFullYear()}-${d.getMonth()}` === key && tx.categoryType === 'Expense'
      })
      .reduce((sum, tx) => sum + Math.abs(tx.amount), 0)
  })

  const padL = 40
  const padR = 20
  const padT = 20
  const padB = 36
  const chartW = W - padL - padR
  const chartH = H - padT - padB
  const maxVal = Math.max(...income, ...expenses, 100) * 1.1
  const barW = (chartW / monthKeys.length) * 0.35
  const groupW = chartW / monthKeys.length

  for (let i = 0; i <= 4; i++) {
    const y = padT + chartH - (i / 4) * chartH
    ctx.beginPath()
    ctx.strokeStyle = dark ? '#2d3f5750' : '#e2e8f050'
    ctx.lineWidth = 1
    ctx.moveTo(padL, y)
    ctx.lineTo(W - padR, y)
    ctx.stroke()

    ctx.fillStyle = dark ? '#64748b' : '#94a3b8'
    ctx.font = '11px Inter, sans-serif'
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
    roundRect(ctx, cx - barW - 3, padT + chartH - incH, barW, incH, 4)
    ctx.fill()

    const grad2 = ctx.createLinearGradient(0, padT + chartH - expH, 0, padT + chartH)
    grad2.addColorStop(0, '#ef4444')
    grad2.addColorStop(1, '#ef444455')
    ctx.fillStyle = grad2
    roundRect(ctx, cx + 3, padT + chartH - expH, barW, expH, 4)
    ctx.fill()

    ctx.fillStyle = dark ? '#94a3b8' : '#64748b'
    ctx.font = '12px Inter, sans-serif'
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

onMounted(async () => {
  await loadData()

  const observer = new MutationObserver(() => nextTick(() => renderCharts()))
  observer.observe(document.querySelector('.app-wrapper') || document.body, {
    attributes: true,
    attributeFilter: ['class'],
  })
})

watch([transactions, categories], async () => {
  await nextTick()
  renderCharts()
}, { deep: true })
</script>

<template>
  <div class="budget-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Budget <span class="title-accent">Übersicht</span></h1>
        <p class="page-subtitle">{{ currentMonth }} · Finanzen auf einen Blick</p>
      </div>
      <div class="page-actions">
        <button class="btn-secondary" @click="loadData" :disabled="loading">Neu laden</button>
        <button class="btn-add" @click="showAddModal = true">
          <span>+</span> Ausgabe hinzufügen
        </button>
      </div>
    </div>

    <div v-if="error" class="alert alert-error">{{ error }}</div>
    <div v-if="success" class="alert alert-success">{{ success }}</div>

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
          <span class="stat-trend trend-down">↓ {{ expensePercent }}% der Einnahmen</span>
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
          <span class="stat-trend trend-up">↑ Ziel: 20%</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
    </div>

    <div class="charts-row">
      <div class="chart-card chart-donut-card">
        <div class="chart-header">
          <span class="chart-title">Kategorien</span>
          <span class="chart-badge">{{ currentMonth }}</span>
        </div>
        <div class="donut-wrapper">
          <canvas ref="donutRef" width="220" height="220"></canvas>
          <div class="donut-center">
            <span class="donut-total">€{{ formatNum(totalExpenses) }}</span>
            <span class="donut-label">Gesamt</span>
          </div>
        </div>
        <div class="legend">
          <div v-for="cat in expenseCategories" :key="cat.id" class="legend-item">
            <span class="legend-dot" :style="{ background: cat.color }"></span>
            <span class="legend-name">{{ cat.name }}</span>
            <span class="legend-val">€{{ formatNum(cat.amount) }}</span>
          </div>
        </div>
      </div>

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

    <div class="budget-limits-card">
      <div class="chart-header">
        <span class="chart-title">Budget-Limits</span>
        <span class="chart-badge warning" v-if="overBudgetCount > 0">{{ overBudgetCount }} überschritten</span>
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
        </div>
        <div v-for="tx in recentTransactions" :key="tx.id" class="trans-row">
          <span class="trans-date">{{ formatDate(tx.date) }}</span>
          <span class="trans-desc">{{ tx.title }}</span>
          <span class="trans-cat">
            <span class="cat-chip" :style="{ background: getCatColor(tx.categoryName) + '22', color: getCatColor(tx.categoryName) }">
              {{ tx.categoryName || '—' }}
            </span>
          </span>
          <span class="trans-amount" :class="tx.categoryType === 'Income' ? 'amount-pos' : 'amount-neg'">
            {{ tx.categoryType === 'Income' ? '+' : '-' }}€{{ Math.abs(tx.amount).toFixed(2) }}
          </span>
        </div>
      </div>
    </div>

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
              <option value="Expense">Expense</option>
              <option value="Income">Income</option>
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
            <span>{{ category.name }}</span>
            <span class="cat-chip small-chip">{{ category.type }}</span>
          </div>
        </div>
      </div>
    </div>

    <Teleport to="body">
      <div v-if="showAddModal" class="modal-backdrop" @click.self="showAddModal = false">
        <div class="modal-box">
          <div class="modal-header">
            <h2>Neue Ausgabe</h2>
            <button class="modal-close" @click="showAddModal = false">✕</button>
          </div>
          <div class="modal-body">
            <div class="form-group">
              <label>Beschreibung</label>
              <input v-model="newTransaction.title" class="form-input" placeholder="z. B. Supermarkt" />
            </div>
            <div class="form-row">
              <div class="form-group">
                <label>Betrag (€)</label>
                <input v-model.number="newTransaction.amount" class="form-input" type="number" step="0.01" placeholder="0.00" />
              </div>
              <div class="form-group">
                <label>Kategorie</label>
                <select v-model="newTransaction.categoryId" class="form-input">
                  <option v-for="cat in categories.filter((c) => c.type === 'Expense')" :key="cat.id" :value="cat.id">
                    {{ cat.name }}
                  </option>
                </select>
              </div>
            </div>
            <div class="form-group">
              <label>Datum</label>
              <input v-model="newTransaction.date" class="form-input" type="datetime-local" />
            </div>
            <div class="form-group">
              <label>Notiz</label>
              <input v-model="newTransaction.note" class="form-input" type="text" placeholder="Optional" />
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
.budget-page {
  max-width: 1200px;
  margin: 0 auto;
  padding: 32px 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 16px;
}

.page-actions {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.page-title {
  font-size: 32px;
  font-weight: 800;
  color: var(--text);
  letter-spacing: -1px;
}

.title-accent {
  color: var(--primary);
}

.page-subtitle {
  color: var(--text-muted);
  font-size: 14px;
  margin-top: 4px;
}

.alert {
  padding: 14px 16px;
  border-radius: var(--radius-sm);
  font-size: 14px;
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

.btn-add,
.btn-save,
.btn-secondary {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 12px 22px;
  border-radius: var(--radius-sm);
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.2s, transform 0.1s;
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
  opacity: 0.95;
  transform: translateY(-1px);
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

@media (max-width: 900px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 540px) {
  .stats-grid {
    grid-template-columns: 1fr;
  }
}

.stat-card {
  position: relative;
  overflow: hidden;
  background: var(--surface);
  border-radius: var(--radius);
  padding: 22px 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: var(--card-shadow);
  border: 1px solid var(--border);
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.stat-card:hover {
  transform: translateY(-2px);
}

.stat-icon {
  font-size: 32px;
  z-index: 1;
}

.stat-info {
  display: flex;
  flex-direction: column;
  gap: 3px;
  z-index: 1;
}

.stat-label {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.stat-value {
  font-size: 22px;
  font-weight: 800;
  color: var(--text);
  letter-spacing: -0.5px;
}

.stat-trend {
  font-size: 11px;
  font-weight: 500;
}

.trend-up { color: #10b981; }
.trend-down { color: #ef4444; }

.stat-bg-shape {
  position: absolute;
  right: -20px;
  top: -20px;
  width: 100px;
  height: 100px;
  border-radius: 50%;
  opacity: 0.06;
}

.stat-income .stat-bg-shape { background: #10b981; }
.stat-expenses .stat-bg-shape { background: #ef4444; }
.stat-balance .stat-bg-shape { background: #6366f1; }
.stat-savings .stat-bg-shape { background: #f59e0b; }

.charts-row {
  display: grid;
  grid-template-columns: 280px 1fr;
  gap: 20px;
}

@media (max-width: 780px) {
  .charts-row {
    grid-template-columns: 1fr;
  }
}

.chart-card,
.budget-limits-card,
.transactions-card,
.management-card {
  background: var(--surface);
  border-radius: var(--radius);
  padding: 24px;
  box-shadow: var(--card-shadow);
  border: 1px solid var(--border);
}

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
  flex-wrap: wrap;
  gap: 8px;
}

.chart-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--text);
}

.chart-badge {
  font-size: 12px;
  padding: 4px 10px;
  background: rgba(99, 102, 241, 0.1);
  color: var(--primary);
  border-radius: 20px;
  font-weight: 600;
}

.chart-badge.warning {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.donut-wrapper {
  position: relative;
  display: flex;
  justify-content: center;
  margin-bottom: 16px;
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
  font-size: 16px;
  font-weight: 800;
  color: var(--text);
}

.donut-label {
  font-size: 11px;
  color: var(--text-muted);
}

.legend {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.legend-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex-shrink: 0;
}

.legend-name {
  flex: 1;
  color: var(--text);
}

.legend-val {
  font-weight: 600;
  color: var(--text);
}

.chart-legend-inline {
  display: flex;
  gap: 16px;
  font-size: 12px;
  color: var(--text-muted);
}

.dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 2px;
  margin-right: 4px;
}

.dot-income { background: #6366f1; }
.dot-expense { background: #ef4444; }
.chart-bar-card canvas { width: 100%; }

.progress-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.progress-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.progress-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.progress-name,
.progress-nums {
  font-size: 13px;
  color: var(--text);
}

.progress-track {
  height: 8px;
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

.trans-table {
  display: flex;
  flex-direction: column;
}

.trans-row {
  display: grid;
  grid-template-columns: 110px 1fr 130px 110px;
  padding: 12px 8px;
  border-bottom: 1px solid var(--border);
  align-items: center;
  font-size: 14px;
}

.trans-row:last-child {
  border-bottom: none;
}

.trans-row:hover:not(.header-row) {
  background: var(--surface2);
  border-radius: 8px;
}

.header-row {
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 2px solid var(--border);
}

.trans-date { color: var(--text-muted); }
.trans-desc { color: var(--text); font-weight: 500; }
.text-right { text-align: right; }
.trans-amount { text-align: right; font-weight: 700; }
.amount-pos { color: #10b981; }
.amount-neg { color: #ef4444; }

.cat-chip {
  display: inline-block;
  padding: 3px 10px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 600;
}

.small-chip {
  background: var(--surface2);
  color: var(--text-muted);
}

.management-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

@media (max-width: 900px) {
  .management-grid {
    grid-template-columns: 1fr;
  }
}

.management-form,
.category-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.category-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 14px;
  border: 1px solid var(--border);
  border-radius: 12px;
  background: var(--surface2);
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-group label {
  font-size: 13px;
  font-weight: 600;
  color: var(--text);
}

.form-input {
  padding: 10px 14px;
  border-radius: 10px;
  border: 1.5px solid var(--border);
  background: var(--surface2);
  color: var(--text);
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s;
}

.form-input:focus {
  border-color: var(--primary);
}

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 999;
  backdrop-filter: blur(4px);
}

.modal-box {
  background: var(--surface);
  border-radius: var(--radius);
  width: 100%;
  max-width: 480px;
  margin: 16px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  border: 1px solid var(--border);
  animation: slideUp 0.25s ease;
}

@keyframes slideUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 1px solid var(--border);
}

.modal-header h2 {
  font-size: 18px;
  font-weight: 700;
  color: var(--text);
}

.modal-close {
  background: none;
  border: none;
  font-size: 18px;
  cursor: pointer;
  color: var(--text-muted);
}

.modal-body {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.modal-footer {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  padding: 16px 24px;
  border-top: 1px solid var(--border);
}

.btn-cancel {
  padding: 10px 20px;
  background: none;
  border: 1.5px solid var(--border);
  color: var(--text);
  border-radius: 10px;
  cursor: pointer;
  font-weight: 600;
  font-size: 14px;
}

@media (max-width: 760px) {
  .budget-page {
    padding: 20px 16px;
  }

  .form-row,
  .trans-row {
    grid-template-columns: 1fr;
  }

  .trans-amount,
  .text-right {
    text-align: left;
  }
}
</style>

