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
  <div class="dashboard-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Inventar <span class="title-accent">Vorräte</span></h1>
        <p class="page-subtitle">Alles, was schon da ist – damit Planner und Einkaufsliste clever gegenrechnen können.</p>
      </div>
    </div>

    <div v-if="error" class="alert alert-error">{{ error }}</div>
    <div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="alert">Lade Inventardaten…</div>

    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-icon">🥫</div>
        <div class="stat-info">
          <span class="stat-label">Einträge</span>
          <span class="stat-value">{{ totalItems }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
    </div>

    <div class="content-grid">
      <div class="form-card">
        <div class="card-header">
          <div>
            <h2 class="card-title">Neuen Inventar-Eintrag anlegen</h2>
            <p class="card-copy">Halte Mengen und Notizen aktuell, damit Wochenplan und Einkaufsliste sauber rechnen.</p>
          </div>
        </div>

        <form @submit.prevent="createItem">
          <div class="form-grid">
            <input v-model="newItem.name" type="text" placeholder="Name" required />
            <input v-model="newItem.quantity" type="number" min="0" step="0.01" placeholder="Menge" required />
            <input v-model="newItem.unit" type="text" placeholder="Einheit" required />
            <textarea v-model="newItem.notes" class="field-span-2" placeholder="Notizen"></textarea>
          </div>

          <div class="form-actions">
            <button class="btn-add" type="submit">Speichern</button>
          </div>
        </form>
      </div>

      <div class="data-card">
        <div class="card-header">
          <div>
            <h2 class="card-title">Inventarübersicht</h2>
            <p class="card-copy">Direkt bearbeitbar und bewusst schlicht gehalten – wie ein digitaler Vorratsschrank.</p>
          </div>
        </div>

        <table v-if="items.length > 0" class="data-table">
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
                <td><input v-model="editItem.name" type="text" /></td>
                <td><input v-model="editItem.quantity" type="number" min="0" step="0.01" /></td>
                <td><input v-model="editItem.unit" type="text" /></td>
                <td><textarea v-model="editItem.notes"></textarea></td>
                <td class="actions">
                  <button class="btn-save" @click="updateItem(item.id)">Speichern</button>
                  <button class="btn-secondary" @click="cancelEditItem">Abbrechen</button>
                </td>
              </template>

              <template v-else>
                <td><strong>{{ item.name }}</strong></td>
                <td>{{ item.quantity }}</td>
                <td>{{ item.unit }}</td>
                <td>{{ item.notes || '—' }}</td>
                <td class="actions">
                  <button class="btn-secondary" @click="startEditItem(item)">Bearbeiten</button>
                  <button class="btn-danger" @click="deleteItem(item.id)">Löschen</button>
                </td>
              </template>
            </tr>
          </tbody>
        </table>

        <div v-else class="empty-state">Noch keine Inventar-Einträge vorhanden.</div>
      </div>
    </div>
  </div>
</template>


<style scoped>
.dashboard-page {
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

.page-title {
  font-size: 32px;
  font-weight: 800;
  color: var(--text);
  letter-spacing: -1px;
  margin: 0;
}

.title-accent { color: var(--primary); }

.page-subtitle {
  color: var(--text-muted);
  font-size: 14px;
  margin-top: 4px;
}

.page-actions,
.header-actions {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.alert {
  padding: 14px 16px;
  border-radius: var(--radius-sm, 12px);
  font-size: 14px;
  border: 1px solid transparent;
}

.alert-error {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
  border-color: rgba(239, 68, 68, 0.2);
}

.alert-success {
  background: rgba(16, 185, 129, 0.1);
  color: #10b981;
  border-color: rgba(16, 185, 129, 0.2);
}

.btn-add,
.btn-save,
.btn-secondary,
.btn-danger,
.btn-ghost,
.small-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 10px 16px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.2s, transform 0.12s ease;
  text-decoration: none;
}

.btn-add,
.btn-save {
  background: var(--primary);
  color: white;
  border: none;
}

.btn-secondary,
.small-button {
  background: var(--surface);
  color: var(--text);
  border: 1px solid var(--border);
}

.btn-danger {
  background: rgba(239,68,68,0.12);
  color: #ef4444;
  border: 1px solid rgba(239,68,68,0.18);
}

.btn-ghost {
  background: transparent;
  color: var(--text);
  border: 1px dashed var(--border);
}

.btn-add:hover,
.btn-save:hover,
.btn-secondary:hover,
.btn-danger:hover,
.btn-ghost:hover,
.small-button:hover {
  opacity: 0.95;
  transform: translateY(-1px);
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

@media (max-width: 900px) {
  .stats-grid { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 560px) {
  .stats-grid { grid-template-columns: 1fr; }
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
}

.stat-icon { font-size: 30px; z-index: 1; }
.stat-info { display: flex; flex-direction: column; gap: 3px; z-index: 1; }
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
.stat-bg-shape {
  position: absolute;
  right: -20px;
  top: -20px;
  width: 100px;
  height: 100px;
  border-radius: 50%;
  opacity: 0.06;
  background: var(--primary);
}

.content-grid {
  display: grid;
  grid-template-columns: minmax(300px, 380px) 1fr;
  gap: 20px;
}

.content-grid.single {
  grid-template-columns: 1fr;
}

@media (max-width: 980px) {
  .content-grid { grid-template-columns: 1fr; }
}

.panel-card,
.form-card,
.data-card,
.side-card,
.week-card,
.list-card,
.sheet-card {
  background: var(--surface);
  border-radius: var(--radius);
  padding: 24px;
  box-shadow: var(--card-shadow);
  border: 1px solid var(--border);
}

.card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 18px;
}

.card-title {
  font-size: 18px;
  font-weight: 800;
  color: var(--text);
  margin: 0;
}

.card-copy,
.hint,
.muted,
.empty-state {
  color: var(--text-muted);
  font-size: 14px;
}

.stack {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0,1fr));
  gap: 12px;
}

.form-grid.full {
  grid-template-columns: 1fr;
}

@media (max-width: 700px) {
  .form-grid { grid-template-columns: 1fr; }
}

.field-span-2 { grid-column: span 2; }
@media (max-width: 700px) { .field-span-2 { grid-column: span 1; } }

.form-actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  margin-top: 8px;
}

input,
textarea,
select,
button {
  font: inherit;
}

input,
textarea,
select {
  width: 100%;
  border-radius: 12px;
  border: 1px solid var(--border);
  background: var(--surface2);
  color: var(--text);
  padding: 12px 14px;
  min-height: 46px;
  box-sizing: border-box;
}

textarea {
  min-height: 104px;
  resize: vertical;
}

.checkbox-row,
.inline-checkbox {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  color: var(--text);
  font-size: 14px;
}

.inline-checkbox input,
.checkbox-row input {
  width: auto;
  min-height: auto;
}

.data-table,
.nested-table,
.paper-table {
  width: 100%;
  border-collapse: collapse;
}

.data-table th,
.data-table td,
.nested-table th,
.nested-table td,
.paper-table th,
.paper-table td {
  padding: 12px 10px;
  border-bottom: 1px solid var(--border);
  vertical-align: top;
  text-align: left;
}

.data-table thead th,
.nested-table thead th,
.paper-table thead th {
  color: var(--text-muted);
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.data-table tbody tr:hover,
.paper-table tbody tr:hover {
  background: rgba(99,102,241,0.04);
}

.actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.status-chip,
.badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border-radius: 999px;
  padding: 5px 10px;
  font-size: 12px;
  font-weight: 700;
  width: fit-content;
}

.badge-primary,
.status-chip.primary { background: rgba(99,102,241,0.12); color: var(--primary); }
.badge-success,
.status-chip.success { background: rgba(16,185,129,0.12); color: #10b981; }
.badge-warning,
.status-chip.warning { background: rgba(245,158,11,0.12); color: #d97706; }
.badge-danger,
.status-chip.danger { background: rgba(239,68,68,0.12); color: #ef4444; }

.empty-state {
  background: var(--surface2);
  border: 1px dashed var(--border);
  border-radius: 14px;
  padding: 20px;
  text-align: center;
}

.split-meta {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}
@media (max-width: 780px) { .split-meta { grid-template-columns: 1fr; } }

.meta-tile {
  background: var(--surface2);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 14px 16px;
}

.meta-label {
  display: block;
  font-size: 12px;
  text-transform: uppercase;
  color: var(--text-muted);
  margin-bottom: 6px;
  letter-spacing: 0.05em;
}

.meta-value {
  font-size: 18px;
  font-weight: 800;
  color: var(--text);
}

.compact-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.compact-item {
  background: var(--surface2);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 14px;
}

.clickable { cursor: pointer; }
.clickable:hover { color: var(--primary); }

@media (max-width: 720px) {
  .data-table,
  .nested-table,
  .paper-table {
    display: block;
    overflow-x: auto;
    white-space: nowrap;
  }
}
</style>
