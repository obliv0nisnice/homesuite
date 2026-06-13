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
  averageBestTotalPrice30d?: number | null
  priceTrendPercent?: number | null
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
  <BContainer class="py-4 d-flex flex-column gap-4">
    <div class="d-flex justify-content-between align-items-end flex-wrap gap-3">
      <div>
        <h1 class="h2 fw-bold mb-1">Katalog <span class="text-primary">Preiszentrale</span></h1>
        <p class="text-secondary mb-0">Artikelstamm, Suchbegriffe und Preisquellen für deine Einkaufslogik.</p>
      </div>

      <div class="d-flex gap-2 flex-wrap">
        <BButton variant="outline-secondary" @click="loadData">Neu laden</BButton>
        <BButton variant="primary" @click="refreshAllPrices">Alle Preise aktualisieren</BButton>
      </div>
    </div>

    <BAlert :model-value="!!error" variant="danger">{{ error }}</BAlert>
    <BAlert :model-value="!!success" variant="success">{{ success }}</BAlert>
    <BAlert :model-value="loading" variant="info">Lade Katalogdaten…</BAlert>

    <BRow class="g-3">
      <BCol md="3">
        <BCard>
          <div class="text-secondary text-uppercase small">Artikel gesamt</div>
          <div class="fs-3 fw-bold">{{ items.length }}</div>
        </BCard>
      </BCol>
      <BCol md="3">
        <BCard>
          <div class="text-secondary text-uppercase small">Grundartikel</div>
          <div class="fs-3 fw-bold">{{ stapleCount }}</div>
        </BCard>
      </BCol>
      <BCol md="3">
        <BCard>
          <div class="text-secondary text-uppercase small">Mit Preisquellen</div>
          <div class="fs-3 fw-bold">{{ pricedCount }}</div>
        </BCard>
      </BCol>
      <BCol md="3">
        <BCard>
          <div class="text-secondary text-uppercase small">Ohne Preisquelle</div>
          <div class="fs-3 fw-bold">{{ items.length - pricedCount }}</div>
        </BCard>
      </BCol>
    </BRow>

    <BRow class="g-4">
      <BCol lg="4">
        <BCard title="Neuen Katalogeintrag anlegen">
          <p class="text-secondary small">Pflege Stammdaten und einen guten Suchbegriff für den Crawler.</p>

          <BForm @submit.prevent="createItem">
            <BFormInput v-model="newItem.name" type="text" placeholder="Name" required class="mb-2" />
            <BFormInput v-model="newItem.defaultUnit" type="text" placeholder="Standard-Einheit" required class="mb-2" />
            <BFormInput v-model="newItem.category" type="text" placeholder="Kategorie" class="mb-2" />
            <BFormInput v-model="newItem.brandHint" type="text" placeholder="Markenhinweis" class="mb-2" />
            <BFormInput
              v-model="newItem.searchTerm"
              type="text"
              placeholder="Crawler-Suchbegriff oder direkte BILLA-URL"
              class="mb-3"
            />

            <div class="d-flex align-items-center gap-3 flex-wrap">
              <BFormCheckbox v-model="newItem.isStaple">Grundartikel</BFormCheckbox>
              <BButton type="submit" variant="primary">Speichern</BButton>
            </div>
          </BForm>
        </BCard>
      </BCol>

      <BCol lg="8">
        <BCard title="Katalogeinträge">
          <p class="text-secondary small">Detailansicht mit Händlerpreisen, Suchbegriffen und schnellen Aktionen.</p>

          <BTableSimple v-if="items.length > 0" hover responsive class="align-middle">
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
                    <td><BFormInput v-model="editItem.name" type="text" /></td>
                    <td><BFormInput v-model="editItem.defaultUnit" type="text" /></td>
                    <td><BFormInput v-model="editItem.category" type="text" /></td>
                    <td><BFormInput v-model="editItem.searchTerm" type="text" /></td>
                    <td><BFormInput v-model="editItem.brandHint" type="text" /></td>
                    <td>{{ formatPrice(item.bestTotalPrice) }}</td>
                    <td>{{ item.prices?.length ?? 0 }}</td>
                    <td>
                      <div class="d-flex gap-2 flex-wrap">
                        <BButton size="sm" variant="primary" @click="updateItem(item.id)">Speichern</BButton>
                        <BButton size="sm" variant="outline-secondary" @click="cancelEditItem">Abbrechen</BButton>
                      </div>
                    </td>
                  </template>

                  <template v-else>
                    <td>
                      <div class="d-flex flex-column gap-1 align-items-start">
                        <strong>{{ item.name }}</strong>
                        <BBadge :variant="item.isStaple ? 'success' : 'primary'">
                          {{ item.isStaple ? 'Grundartikel' : 'Standardartikel' }}
                        </BBadge>
                      </div>
                    </td>
                    <td>{{ item.defaultUnit }}</td>
                    <td>{{ item.category || '—' }}</td>
                    <td>{{ item.searchTerm || '—' }}</td>
                    <td>{{ item.brandHint || '—' }}</td>
                    <td>
                      {{ formatPrice(item.bestTotalPrice) }}
                      <BBadge
                        v-if="item.priceTrendPercent != null && Math.abs(item.priceTrendPercent) >= 2"
                        pill
                        :variant="item.priceTrendPercent < 0 ? 'success' : 'danger'"
                        :title="`30-Tage-Schnitt: ${formatPrice(item.averageBestTotalPrice30d)}`"
                      >
                        {{ item.priceTrendPercent < 0 ? '▼' : '▲' }} {{ Math.abs(item.priceTrendPercent).toFixed(0) }}%
                      </BBadge>
                    </td>
                    <td>{{ item.prices?.length ?? 0 }}</td>
                    <td>
                      <div class="d-flex gap-2 flex-wrap">
                        <BButton size="sm" variant="outline-secondary" @click="toggleDetails(item.id)">
                          {{ expandedItemId === item.id ? 'Details ausblenden' : 'Details' }}
                        </BButton>
                        <BButton size="sm" variant="outline-primary" @click="refreshItemPrices(item.id)">Preise holen</BButton>
                        <BButton size="sm" variant="outline-secondary" @click="startEditItem(item)">Bearbeiten</BButton>
                        <BButton size="sm" variant="outline-danger" @click="deleteItem(item.id)">Löschen</BButton>
                      </div>
                    </td>
                  </template>
                </tr>

                <tr v-if="expandedItemId === item.id">
                  <td colspan="8">
                    <BRow class="g-3">
                      <BCol md="3">
                        <BCard body-class="py-2">
                          <div class="text-secondary text-uppercase small">Bester Einheitspreis</div>
                          <div class="fw-bold">{{ formatPrice(item.bestUnitPrice) }}</div>
                        </BCard>
                      </BCol>
                      <BCol md="3">
                        <BCard body-class="py-2">
                          <div class="text-secondary text-uppercase small">Bester Gesamtpreis</div>
                          <div class="fw-bold">{{ formatPrice(item.bestTotalPrice) }}</div>
                        </BCard>
                      </BCol>
                      <BCol md="3" v-if="item.averageBestTotalPrice30d != null">
                        <BCard body-class="py-2">
                          <div class="text-secondary text-uppercase small">30-Tage-Schnitt</div>
                          <div class="fw-bold">{{ formatPrice(item.averageBestTotalPrice30d) }}</div>
                        </BCard>
                      </BCol>
                      <BCol md="3">
                        <BCard body-class="py-2">
                          <div class="text-secondary text-uppercase small">Suchbegriff</div>
                          <div class="fw-bold">{{ item.searchTerm || '—' }}</div>
                        </BCard>
                      </BCol>
                    </BRow>

                    <BTableSimple v-if="item.prices && item.prices.length > 0" small responsive class="mt-3 mb-0">
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
                    </BTableSimple>

                    <p v-else class="text-secondary mt-3 mb-0">Noch keine Preisquellen vorhanden.</p>
                  </td>
                </tr>
              </template>
            </tbody>
          </BTableSimple>

          <p v-else class="text-secondary mb-0">Noch keine Katalogeinträge vorhanden.</p>
        </BCard>
      </BCol>
    </BRow>
  </BContainer>
</template>
