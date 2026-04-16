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
  <section>
    <div class="headline-row">
      <div>
        <h2>Katalog</h2>
        <p class="muted">
          Pflege Artikelstammdaten und teste den Crawler per Freitext oder direkter BILLA-URL.
        </p>
      </div>
      <button @click="refreshAllPrices">Alle Preise aktualisieren</button>
    </div>

    <p v-if="loading">Lade Daten...</p>
    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="success" class="success">{{ success }}</p>

    <div class="summary-grid">
      <div class="card">
        <h3>Artikel gesamt</h3>
        <p class="big">{{ items.length }}</p>
      </div>
      <div class="card">
        <h3>Grundartikel</h3>
        <p class="big">{{ stapleCount }}</p>
      </div>
      <div class="card">
        <h3>Mit Preisquellen</h3>
        <p class="big">{{ pricedCount }}</p>
      </div>
    </div>

    <div class="card">
      <h3>Neuen Katalogeintrag anlegen</h3>

      <form class="form" @submit.prevent="createItem">
        <input v-model="newItem.name" type="text" placeholder="Name" required />
        <input v-model="newItem.defaultUnit" type="text" placeholder="Standard-Einheit" required />
        <input v-model="newItem.category" type="text" placeholder="Kategorie" />
        <input
          v-model="newItem.searchTerm"
          type="text"
          placeholder="Crawler-Suchbegriff oder direkte BILLA-URL"
        />
        <input v-model="newItem.brandHint" type="text" placeholder="Markenhinweis" />

        <label class="checkbox-row">
          <input v-model="newItem.isStaple" type="checkbox" />
          Grundartikel
        </label>

        <button type="submit">Speichern</button>
      </form>
    </div>

    <div class="card">
      <div class="table-header">
        <h3>Katalogeinträge</h3>
        <button @click="loadData">Neu laden</button>
      </div>

      <table v-if="items.length > 0">
        <thead>
          <tr>
            <th>Name</th>
            <th>Einheit</th>
            <th>Kategorie</th>
            <th>Suchbegriff</th>
            <th>Marke</th>
            <th>Grundartikel</th>
            <th>Bester Preis</th>
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
                <td>
                  <input v-model="editItem.isStaple" type="checkbox" />
                </td>
                <td>{{ formatPrice(item.bestTotalPrice) }}</td>
                <td>{{ item.prices?.length ?? 0 }}</td>
                <td class="actions">
                  <button @click="updateItem(item.id)">Speichern</button>
                  <button @click="cancelEditItem">Abbrechen</button>
                </td>
              </template>

              <template v-else>
                <td>{{ item.name }}</td>
                <td>{{ item.defaultUnit }}</td>
                <td>{{ item.category || '—' }}</td>
                <td class="search-term-cell">{{ item.searchTerm || '—' }}</td>
                <td>{{ item.brandHint || '—' }}</td>
                <td>{{ item.isStaple ? 'Ja' : 'Nein' }}</td>
                <td>{{ formatPrice(item.bestTotalPrice) }}</td>
                <td>{{ item.prices?.length ?? 0 }}</td>
                <td class="actions">
                  <button @click="toggleDetails(item.id)">
                    {{ expandedItemId === item.id ? 'Details ausblenden' : 'Details' }}
                  </button>
                  <button @click="refreshItemPrices(item.id)">Preise holen</button>
                  <button @click="startEditItem(item)">Bearbeiten</button>
                  <button @click="deleteItem(item.id)">Löschen</button>
                </td>
              </template>
            </tr>

            <tr v-if="expandedItemId === item.id" class="details-row">
              <td colspan="9">
                <div class="details-box">
                  <div class="details-meta">
                    <div><strong>Bester Einheitspreis:</strong> {{ formatPrice(item.bestUnitPrice) }}</div>
                    <div><strong>Bester Gesamtpreis:</strong> {{ formatPrice(item.bestTotalPrice) }}</div>
                    <div><strong>Suchbegriff:</strong> {{ item.searchTerm || '—' }}</div>
                  </div>

                  <table v-if="item.prices && item.prices.length > 0" class="nested-table">
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
                          <a
                            v-if="price.productUrl"
                            :href="price.productUrl"
                            target="_blank"
                            rel="noopener noreferrer"
                          >
                            Öffnen
                          </a>
                          <span v-else>—</span>
                        </td>
                      </tr>
                    </tbody>
                  </table>

                  <p v-else class="muted">Noch keine Preisquellen vorhanden.</p>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>

      <p v-else>Noch keine Katalogeinträge vorhanden.</p>
    </div>
  </section>
</template>

<style scoped>
.headline-row {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  align-items: flex-start;
  margin-bottom: 16px;
}

.muted {
  color: #666;
  margin-top: 4px;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
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
button {
  padding: 10px;
  font: inherit;
}

.checkbox-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.table-header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: center;
  margin-bottom: 12px;
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

.search-term-cell {
  max-width: 260px;
  word-break: break-word;
}

.actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.details-row td {
  background: #fafcff;
}

.details-box {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.details-meta {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
}

.nested-table {
  border: 1px solid #e6e6e6;
}

.error {
  color: #b00020;
  margin-bottom: 16px;
}

.success {
  color: #0a7a2f;
  margin-bottom: 16px;
}

a {
  color: #0b57d0;
}

@media (max-width: 900px) {
  .summary-grid {
    grid-template-columns: 1fr;
  }

  .headline-row,
  .table-header {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>

