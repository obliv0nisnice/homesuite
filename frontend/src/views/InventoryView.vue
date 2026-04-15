<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '@/services/api'

type InventoryItem = {
  id: string
  name: string
  quantity: number
  unit: string
  notes?: string | null
}

const items = ref<InventoryItem[]>([])
const loading = ref(false)
const error = ref('')
const success = ref('')

const newItem = ref({
  name: '',
  quantity: 0,
  unit: 'Stk',
  notes: '',
})

const editingItemId = ref<string | null>(null)
const editItem = ref({
  name: '',
  quantity: 0,
  unit: 'Stk',
  notes: '',
})

const totalItems = computed(() => items.value.length)

async function loadData() {
  loading.value = true
  error.value = ''
  success.value = ''

  try {
    items.value = await apiFetch<InventoryItem[]>('/inventory')
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Inventar konnte nicht geladen werden.'
  } finally {
    loading.value = false
  }
}

async function createItem() {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<InventoryItem>('/inventory', {
      method: 'POST',
      body: JSON.stringify({
        ...newItem.value,
        quantity: Number(newItem.value.quantity),
      }),
    })

    newItem.value = {
      name: '',
      quantity: 0,
      unit: 'Stk',
      notes: '',
    }

    success.value = 'Inventar-Eintrag wurde erstellt.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Inventar-Eintrag konnte nicht erstellt werden.'
  }
}

function startEditItem(item: InventoryItem) {
  editingItemId.value = item.id
  editItem.value = {
    name: item.name,
    quantity: item.quantity,
    unit: item.unit,
    notes: item.notes ?? '',
  }
}

function cancelEditItem() {
  editingItemId.value = null
  editItem.value = {
    name: '',
    quantity: 0,
    unit: 'Stk',
    notes: '',
  }
}

async function updateItem(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<InventoryItem>(`/inventory/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        ...editItem.value,
        quantity: Number(editItem.value.quantity),
      }),
    })

    cancelEditItem()
    success.value = 'Inventar-Eintrag wurde aktualisiert.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Inventar-Eintrag konnte nicht aktualisiert werden.'
  }
}

async function deleteItem(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>(`/inventory/${id}`, {
      method: 'DELETE',
    })

    if (editingItemId.value === id) {
      cancelEditItem()
    }

    success.value = 'Inventar-Eintrag wurde gelöscht.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Inventar-Eintrag konnte nicht gelöscht werden.'
  }
}

onMounted(loadData)
</script>

<template>
  <section>
    <h2>Inventar</h2>

    <p v-if="loading">Lade Daten...</p>
    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="success" class="success">{{ success }}</p>

    <div class="summary-grid">
      <div class="card">
        <h3>Einträge</h3>
        <p class="big">{{ totalItems }}</p>
      </div>
    </div>

    <div class="card">
      <h3>Neuen Inventar-Eintrag anlegen</h3>

      <form class="form" @submit.prevent="createItem">
        <input v-model="newItem.name" type="text" placeholder="Name" required />
        <input
          v-model="newItem.quantity"
          type="number"
          min="0"
          step="0.01"
          placeholder="Menge"
          required
        />
        <input v-model="newItem.unit" type="text" placeholder="Einheit" required />
        <textarea v-model="newItem.notes" placeholder="Notizen"></textarea>
        <button type="submit">Speichern</button>
      </form>
    </div>

    <div class="card">
      <h3>Inventar</h3>

      <table v-if="items.length > 0">
        <thead>
          <tr>
            <th>Name</th>
            <th>Menge</th>
            <th>Einheit</th>
            <th>Notizen</th>
            <th>Aktionen</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in items" :key="item.id">
            <template v-if="editingItemId === item.id">
              <td>
                <input v-model="editItem.name" type="text" />
              </td>
              <td>
                <input v-model="editItem.quantity" type="number" min="0" step="0.01" />
              </td>
              <td>
                <input v-model="editItem.unit" type="text" />
              </td>
              <td>
                <textarea v-model="editItem.notes"></textarea>
              </td>
              <td class="actions">
                <button @click="updateItem(item.id)">Speichern</button>
                <button @click="cancelEditItem">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td>{{ item.name }}</td>
              <td>{{ item.quantity }}</td>
              <td>{{ item.unit }}</td>
              <td>{{ item.notes || '—' }}</td>
              <td class="actions">
                <button @click="startEditItem(item)">Bearbeiten</button>
                <button @click="deleteItem(item.id)">Löschen</button>
              </td>
            </template>
          </tr>
        </tbody>
      </table>

      <p v-else>Noch keine Inventar-Einträge vorhanden.</p>
    </div>
  </section>
</template>

<style scoped>
.summary-grid {
  display: grid;
  grid-template-columns: repeat(1, 1fr);
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
textarea,
button {
  padding: 10px;
  font: inherit;
}

textarea {
  min-height: 80px;
  resize: vertical;
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
  vertical-align: top;
}

.actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.error {
  color: #b00020;
  margin-bottom: 16px;
}

.success {
  color: #0a7a2f;
  margin-bottom: 16px;
}
</style>
