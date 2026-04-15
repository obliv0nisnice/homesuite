<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
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

const categories = ref<Category[]>([])
const transactions = ref<Transaction[]>([])

const loading = ref(false)
const error = ref('')

const newCategory = ref({
  name: '',
  type: 'Expense',
})

const editingCategoryId = ref<string | null>(null)
const editCategory = ref({
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

const editingTransactionId = ref<string | null>(null)
const editTransaction = ref({
  title: '',
  amount: 0,
  date: '',
  note: '',
  categoryId: '',
})

const income = computed(() =>
  transactions.value
    .filter((t) => t.categoryType === 'Income')
    .reduce((sum, t) => sum + t.amount, 0),
)

const expenses = computed(() =>
  transactions.value
    .filter((t) => t.categoryType === 'Expense')
    .reduce((sum, t) => sum + t.amount, 0),
)

const balance = computed(() => income.value - expenses.value)

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

    if (!newTransaction.value.categoryId && loadedCategories.length > 0) {
      newTransaction.value.categoryId = loadedCategories[0].id
    }
  } catch (err) {
    console.error(err)
    error.value = 'Daten konnten nicht geladen werden.'
  } finally {
    loading.value = false
  }
}

async function createCategory() {
  error.value = ''

  try {
    await apiFetch<Category>('/categories', {
      method: 'POST',
      body: JSON.stringify(newCategory.value),
    })

    newCategory.value = {
      name: '',
      type: 'Expense',
    }

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = 'Kategorie konnte nicht erstellt werden.'
  }
}

function startEditCategory(category: Category) {
  editingCategoryId.value = category.id
  editCategory.value = {
    name: category.name,
    type: category.type,
  }
}

function cancelEditCategory() {
  editingCategoryId.value = null
  editCategory.value = {
    name: '',
    type: 'Expense',
  }
}

async function updateCategory(id: string) {
  error.value = ''

  try {
    await apiFetch<Category>(`/categories/${id}`, {
      method: 'PUT',
      body: JSON.stringify(editCategory.value),
    })

    cancelEditCategory()
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = 'Kategorie konnte nicht aktualisiert werden.'
  }
}

async function deleteCategory(id: string) {
  error.value = ''

  try {
    await apiFetch<void>(`/categories/${id}`, {
      method: 'DELETE',
    })

    if (editingCategoryId.value === id) {
      cancelEditCategory()
    }

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = 'Kategorie konnte nicht gelöscht werden.'
  }
}

async function createTransaction() {
  error.value = ''

  try {
    await apiFetch<Transaction>('/transactions', {
      method: 'POST',
      body: JSON.stringify({
        ...newTransaction.value,
        amount: Number(newTransaction.value.amount),
        date: new Date(newTransaction.value.date).toISOString(),
      }),
    })

    newTransaction.value = {
      title: '',
      amount: 0,
      date: new Date().toISOString().slice(0, 16),
      note: '',
      categoryId: categories.value[0]?.id ?? '',
    }

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = 'Transaktion konnte nicht erstellt werden.'
  }
}

function startEditTransaction(transaction: Transaction) {
  editingTransactionId.value = transaction.id
  editTransaction.value = {
    title: transaction.title,
    amount: transaction.amount,
    date: new Date(transaction.date).toISOString().slice(0, 16),
    note: transaction.note ?? '',
    categoryId: transaction.categoryId,
  }
}

function cancelEditTransaction() {
  editingTransactionId.value = null
  editTransaction.value = {
    title: '',
    amount: 0,
    date: '',
    note: '',
    categoryId: '',
  }
}

async function updateTransaction(id: string) {
  error.value = ''

  try {
    await apiFetch<Transaction>(`/transactions/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        ...editTransaction.value,
        amount: Number(editTransaction.value.amount),
        date: new Date(editTransaction.value.date).toISOString(),
      }),
    })

    cancelEditTransaction()
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = 'Transaktion konnte nicht aktualisiert werden.'
  }
}

async function deleteTransaction(id: string) {
  error.value = ''

  try {
    await apiFetch<void>(`/transactions/${id}`, {
      method: 'DELETE',
    })

    if (editingTransactionId.value === id) {
      cancelEditTransaction()
    }

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = 'Transaktion konnte nicht gelöscht werden.'
  }
}

onMounted(loadData)
</script>

<template>
  <section>
    <h2>Budget</h2>

    <p v-if="loading">Lade Daten...</p>
    <p v-if="error" class="error">{{ error }}</p>

    <div class="summary-grid">
      <div class="card">
        <h3>Saldo</h3>
        <p class="big">{{ balance.toFixed(2) }} €</p>
      </div>

      <div class="card">
        <h3>Einnahmen</h3>
        <p class="big">{{ income.toFixed(2) }} €</p>
      </div>

      <div class="card">
        <h3>Ausgaben</h3>
        <p class="big">{{ expenses.toFixed(2) }} €</p>
      </div>
    </div>

    <div class="grid">
      <div class="card">
        <h3>Kategorie anlegen</h3>

        <form class="form" @submit.prevent="createCategory">
          <input v-model="newCategory.name" type="text" placeholder="Name" required />

          <select v-model="newCategory.type">
            <option value="Expense">Expense</option>
            <option value="Income">Income</option>
          </select>

          <button type="submit">Speichern</button>
        </form>
      </div>

      <div class="card">
        <h3>Transaktion anlegen</h3>

        <form class="form" @submit.prevent="createTransaction">
          <input v-model="newTransaction.title" type="text" placeholder="Titel" required />

          <input
            v-model="newTransaction.amount"
            type="number"
            step="0.01"
            placeholder="Betrag"
            required
          />

          <input v-model="newTransaction.date" type="datetime-local" required />

          <input v-model="newTransaction.note" type="text" placeholder="Notiz" />

          <select v-model="newTransaction.categoryId" required>
            <option disabled value="">Kategorie wählen</option>
            <option
              v-for="category in categories"
              :key="category.id"
              :value="category.id"
            >
              {{ category.name }} ({{ category.type }})
            </option>
          </select>

          <button type="submit">Speichern</button>
        </form>
      </div>
    </div>

    <div class="card">
      <h3>Kategorien</h3>

      <table v-if="categories.length > 0">
        <thead>
          <tr>
            <th>Name</th>
            <th>Typ</th>
            <th>Aktionen</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="category in categories" :key="category.id">
            <template v-if="editingCategoryId === category.id">
              <td>
                <input v-model="editCategory.name" type="text" />
              </td>
              <td>
                <select v-model="editCategory.type">
                  <option value="Expense">Expense</option>
                  <option value="Income">Income</option>
                </select>
              </td>
              <td class="actions">
                <button @click="updateCategory(category.id)">Speichern</button>
                <button @click="cancelEditCategory">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td>{{ category.name }}</td>
              <td>{{ category.type }}</td>
              <td class="actions">
                <button @click="startEditCategory(category)">Bearbeiten</button>
                <button @click="deleteCategory(category.id)">Löschen</button>
              </td>
            </template>
          </tr>
        </tbody>
      </table>

      <p v-else>Noch keine Kategorien vorhanden.</p>
    </div>

    <div class="card">
      <h3>Transaktionen</h3>

      <table v-if="transactions.length > 0">
        <thead>
          <tr>
            <th>Titel</th>
            <th>Betrag</th>
            <th>Datum</th>
            <th>Kategorie</th>
            <th>Notiz</th>
            <th>Aktionen</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="transaction in transactions" :key="transaction.id">
            <template v-if="editingTransactionId === transaction.id">
              <td>
                <input v-model="editTransaction.title" type="text" />
              </td>
              <td>
                <input v-model="editTransaction.amount" type="number" step="0.01" />
              </td>
              <td>
                <input v-model="editTransaction.date" type="datetime-local" />
              </td>
              <td>
                <select v-model="editTransaction.categoryId">
                  <option
                    v-for="category in categories"
                    :key="category.id"
                    :value="category.id"
                  >
                    {{ category.name }} ({{ category.type }})
                  </option>
                </select>
              </td>
              <td>
                <input v-model="editTransaction.note" type="text" />
              </td>
              <td class="actions">
                <button @click="updateTransaction(transaction.id)">Speichern</button>
                <button @click="cancelEditTransaction">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td>{{ transaction.title }}</td>
              <td :class="transaction.categoryType === 'Expense' ? 'expense' : 'income'">
                {{ transaction.amount.toFixed(2) }} €
              </td>
              <td>{{ new Date(transaction.date).toLocaleString() }}</td>
              <td>{{ transaction.categoryName || '—' }}</td>
              <td>{{ transaction.note || '—' }}</td>
              <td class="actions">
                <button @click="startEditTransaction(transaction)">Bearbeiten</button>
                <button @click="deleteTransaction(transaction.id)">Löschen</button>
              </td>
            </template>
          </tr>
        </tbody>
      </table>

      <p v-else>Noch keine Transaktionen vorhanden.</p>
    </div>
  </section>
</template>

<style scoped>
.summary-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 16px;
}

.grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin-bottom: 16px;
}

.card {
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 16px;
  margin-bottom: 16px;
}

.big {
  font-size: 1.5rem;
  font-weight: 700;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

input,
select,
button {
  padding: 10px;
  font: inherit;
}

table {
  width: 100%;
  border-collapse: collapse;
}

th,
td {
  padding: 10px;
  border-bottom: 1px solid #ddd;
  text-align: left;
  vertical-align: middle;
}

.actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.expense {
  color: #b00020;
  font-weight: 600;
}

.income {
  color: #0a7a2f;
  font-weight: 600;
}

.error {
  color: #b00020;
  margin-bottom: 16px;
}

@media (max-width: 900px) {
  .summary-grid,
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
