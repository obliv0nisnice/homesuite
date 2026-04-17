<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '@/services/api'

type CatalogItemPrice = {
  id: string
  storeName: string
  productName: string
  unitPrice?: number | null
  totalPrice?: number | null
  productUrl?: string | null
  isAvailable: boolean
  checkedAt: string
  sourceType: string
}

type CatalogItem = {
  id: string
  name: string
  defaultUnit: string
  category?: string | null
  searchTerm?: string | null
  brandHint?: string | null
  isStaple: boolean
  prices: CatalogItemPrice[]
  bestUnitPrice?: number | null
  bestTotalPrice?: number | null
}

type CatalogItemPayload = {
  name: string
  defaultUnit: string
  category?: string | null
  searchTerm?: string | null
  brandHint?: string | null
  isStaple: boolean
}

const items = ref<CatalogItem[]>([])
const loading = ref(false)
const error = ref('')
const success = ref('')
const expandedItemId = ref<string | null>(null)

const newItem = ref({
  name: '',
  defaultUnit: 'Stk',
  category: '',
  searchTerm: '',
  brandHint: '',
  isStaple: true,
})

const editingItemId = ref<string | null>(null)
const editItem = ref({
  name: '',
  defaultUnit: 'Stk',
  category: '',
  searchTerm: '',
  brandHint: '',
  isStaple: true,
})

const stapleCount = computed(() => items.value.filter((x) => x.isStaple).length)
const pricedCount = computed(() => items.value.filter((x) => (x.prices?.length ?? 0) > 0).length)

function normalizePayload(source: typeof newItem.value): CatalogItemPayload {
  return {
    name: source.name.trim(),
    defaultUnit: source.defaultUnit.trim(),
    category: source.category.trim() || null,
    searchTerm: source.searchTerm.trim() || null,
    brandHint: source.brandHint.trim() || null,
    isStaple: source.isStaple,
  }
}

function formatPrice(value?: number | null): string {
  if (value == null) {
    return '—'
  }

  return `${value.toFixed(2)} €`
}

function formatDate(value?: string | null): string {
  if (!value) {
    return '—'
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return new Intl.DateTimeFormat('de-AT', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(date)
}

function resetNewItem() {
  newItem.value = {
    name: '',
    defaultUnit: 'Stk',
    category: '',
    searchTerm: '',
    brandHint: '',
    isStaple: true,
  }
}

async function loadData() {
  loading.value = true
  error.value = ''

  try {
    items.value = await apiFetch<CatalogItem[]>('/catalog')
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Katalog konnte nicht geladen werden.'
  } finally {
    loading.value = false
  }
}

async function createItem() {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<CatalogItem>('/catalog', {
      method: 'POST',
      body: JSON.stringify(normalizePayload(newItem.value)),
    })

    resetNewItem()
    success.value = 'Katalogeintrag wurde erstellt.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Katalogeintrag konnte nicht erstellt werden.'
  }
}

function startEditItem(item: CatalogItem) {
  editingItemId.value = item.id
  editItem.value = {
    name: item.name,
    defaultUnit: item.defaultUnit,
    category: item.category ?? '',
    searchTerm: item.searchTerm ?? '',
    brandHint: item.brandHint ?? '',
    isStaple: item.isStaple,
  }
}

function cancelEditItem() {
  editingItemId.value = null
  editItem.value = {
    name: '',
    defaultUnit: 'Stk',
    category: '',
    searchTerm: '',
    brandHint: '',
    isStaple: true,
  }
}

function toggleDetails(id: string) {
  expandedItemId.value = expandedItemId.value === id ? null : id
}

async function updateItem(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<CatalogItem>(`/catalog/${id}`, {
      method: 'PUT',
      body: JSON.stringify(normalizePayload(editItem.value)),
    })

    cancelEditItem()
    success.value = 'Katalogeintrag wurde aktualisiert.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Katalogeintrag konnte nicht aktualisiert werden.'
  }
}

async function deleteItem(id: string) {
  error.value = ''
  success.value = ''

  const confirmed = window.confirm('Katalogeintrag wirklich löschen?')
  if (!confirmed) {
    return
  }

  try {
    await apiFetch<void>(`/catalog/${id}`, {
      method: 'DELETE',
    })

    if (editingItemId.value === id) {
      cancelEditItem()
    }

    if (expandedItemId.value === id) {
      expandedItemId.value = null
    }

    success.value = 'Katalogeintrag wurde gelöscht.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Katalogeintrag konnte nicht gelöscht werden.'
  }
}

async function refreshItemPrices(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>(`/catalog/${id}/refresh-prices`, {
      method: 'POST',
    })

    expandedItemId.value = id
    success.value = 'Preise wurden aktualisiert.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Preise konnten nicht aktualisiert werden.'
  }
}

async function refreshAllPrices() {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>('/catalog/refresh-prices', {
      method: 'POST',
    })

    success.value = 'Preise für den gesamten Katalog wurden aktualisiert.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Preise konnten nicht aktualisiert werden.'
  }
}

onMounted(loadData)
</script>


<template>
  <div class="dashboard-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Katalog <span class="title-accent">Preiszentrale</span></h1>
        <p class="page-subtitle">Artikelstamm, Suchbegriffe und Preisquellen für deine Einkaufslogik.</p>
      </div>

      <div class="page-actions">
        <button class="btn-secondary" @click="loadData">Neu laden</button>
        <button class="btn-add" @click="refreshAllPrices">Alle Preise aktualisieren</button>
      </div>
    </div>

    <div v-if="error" class="alert alert-error">{{ error }}</div>
    <div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="alert">Lade Katalogdaten…</div>

    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-icon">📦</div>
        <div class="stat-info">
          <span class="stat-label">Artikel gesamt</span>
          <span class="stat-value">{{ items.length }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">🥫</div>
        <div class="stat-info">
          <span class="stat-label">Grundartikel</span>
          <span class="stat-value">{{ stapleCount }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">🏷️</div>
        <div class="stat-info">
          <span class="stat-label">Mit Preisquellen</span>
          <span class="stat-value">{{ pricedCount }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">⚡</div>
        <div class="stat-info">
          <span class="stat-label">Ohne Preisquelle</span>
          <span class="stat-value">{{ items.length - pricedCount }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
    </div>

    <div class="content-grid">
      <div class="stack">
        <div class="form-card">
          <div class="card-header">
            <div>
              <h2 class="card-title">Neuen Katalogeintrag anlegen</h2>
              <p class="card-copy">Pflege Stammdaten und einen guten Suchbegriff für den Crawler.</p>
            </div>
          </div>

          <form @submit.prevent="createItem">
            <div class="form-grid">
              <input v-model="newItem.name" type="text" placeholder="Name" required />
              <input v-model="newItem.defaultUnit" type="text" placeholder="Standard-Einheit" required />
              <input v-model="newItem.category" type="text" placeholder="Kategorie" />
              <input v-model="newItem.brandHint" type="text" placeholder="Markenhinweis" />
              <input
                v-model="newItem.searchTerm"
                class="field-span-2"
                type="text"
                placeholder="Crawler-Suchbegriff oder direkte BILLA-URL"
              />
            </div>

            <div class="form-actions">
              <label class="checkbox-row">
                <input v-model="newItem.isStaple" type="checkbox" />
                Grundartikel
              </label>
              <button class="btn-add" type="submit">Speichern</button>
            </div>
          </form>
        </div>
      </div>

      <div class="data-card">
        <div class="card-header">
          <div>
            <h2 class="card-title">Katalogeinträge</h2>
            <p class="card-copy">Detailansicht mit Händlerpreisen, Suchbegriffen und schnellen Aktionen.</p>
          </div>
        </div>

        <table v-if="items.length > 0" class="data-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Einheit</th>
              <th>Kategorie</th>
              <th>Suchbegriff</th>
              <th>Marke</th>
              <th>Bestpreis</th>
              <th>Quellen</th>
              <th>Aktionen</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="item in items" :key="item.id">
              <tr>
                <template v-if="editingItemId === item.id">
                  <td><input v-model="editItem.name" type="text" /></td>
                  <td><input v-model="editItem.defaultUnit" type="text" /></td>
                  <td><input v-model="editItem.category" type="text" /></td>
                  <td><input v-model="editItem.searchTerm" type="text" /></td>
                  <td><input v-model="editItem.brandHint" type="text" /></td>
                  <td>{{ formatPrice(item.bestTotalPrice) }}</td>
                  <td>{{ item.prices?.length ?? 0 }}</td>
                  <td class="actions">
                    <button class="btn-save" @click="updateItem(item.id)">Speichern</button>
                    <button class="btn-secondary" @click="cancelEditItem">Abbrechen</button>
                  </td>
                </template>

                <template v-else>
                  <td>
                    <div class="compact-list">
                      <strong>{{ item.name }}</strong>
                      <span :class="['badge', item.isStaple ? 'badge-success' : 'badge-primary']">
                        {{ item.isStaple ? 'Grundartikel' : 'Standardartikel' }}
                      </span>
                    </div>
                  </td>
                  <td>{{ item.defaultUnit }}</td>
                  <td>{{ item.category || '—' }}</td>
                  <td>{{ item.searchTerm || '—' }}</td>
                  <td>{{ item.brandHint || '—' }}</td>
                  <td>{{ formatPrice(item.bestTotalPrice) }}</td>
                  <td>{{ item.prices?.length ?? 0 }}</td>
                  <td class="actions">
                    <button class="btn-secondary" @click="toggleDetails(item.id)">
                      {{ expandedItemId === item.id ? 'Details ausblenden' : 'Details' }}
                    </button>
                    <button class="btn-ghost" @click="refreshItemPrices(item.id)">Preise holen</button>
                    <button class="btn-secondary" @click="startEditItem(item)">Bearbeiten</button>
                    <button class="btn-danger" @click="deleteItem(item.id)">Löschen</button>
                  </td>
                </template>
              </tr>

              <tr v-if="expandedItemId === item.id">
                <td colspan="8">
                  <div class="compact-item">
                    <div class="split-meta">
                      <div class="meta-tile">
                        <span class="meta-label">Bester Einheitspreis</span>
                        <span class="meta-value">{{ formatPrice(item.bestUnitPrice) }}</span>
                      </div>
                      <div class="meta-tile">
                        <span class="meta-label">Bester Gesamtpreis</span>
                        <span class="meta-value">{{ formatPrice(item.bestTotalPrice) }}</span>
                      </div>
                      <div class="meta-tile">
                        <span class="meta-label">Suchbegriff</span>
                        <span class="meta-value" style="font-size: 14px;">{{ item.searchTerm || '—' }}</span>
                      </div>
                    </div>

                    <table v-if="item.prices && item.prices.length > 0" class="nested-table" style="margin-top:16px;">
                      <thead>
                        <tr>
                          <th>Shop</th>
                          <th>Produkt</th>
                          <th>Einzelpreis</th>
                          <th>Gesamtpreis</th>
                          <th>Stand</th>
                          <th>Quelle</th>
                          <th>Link</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="price in item.prices" :key="price.id">
                          <td>{{ price.storeName }}</td>
                          <td>{{ price.productName }}</td>
                          <td>{{ formatPrice(price.unitPrice) }}</td>
                          <td>{{ formatPrice(price.totalPrice) }}</td>
                          <td>{{ formatDate(price.checkedAt) }}</td>
                          <td>{{ price.sourceType }}</td>
                          <td>
                            <a v-if="price.productUrl" :href="price.productUrl" target="_blank" rel="noopener noreferrer">Öffnen</a>
                            <span v-else>—</span>
                          </td>
                        </tr>
                      </tbody>
                    </table>

                    <div v-else class="empty-state" style="margin-top:16px;">Noch keine Preisquellen vorhanden.</div>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </table>

        <div v-else class="empty-state">Noch keine Katalogeinträge vorhanden.</div>
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
