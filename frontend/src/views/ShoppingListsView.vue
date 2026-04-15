<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '@/services/api'

type CatalogItem = {
  id: string
  name: string
  defaultUnit: string
  category?: string | null
  searchTerm?: string | null
  brandHint?: string | null
  isStaple: boolean
}

type ShoppingItemPriceOption = {
  id: string
  shoppingItemId: string
  storeName: string
  productName: string
  unitPrice: number
  totalPrice: number
  productUrl?: string | null
  isAvailable: boolean
  checkedAt: string
}

type ShoppingItem = {
  id: string
  name: string
  requiredQuantity: number
  inventoryQuantityUsed: number
  quantity: number
  purchasedQuantity: number
  unit: string
  isChecked: boolean
  estimatedUnitPrice?: number | null
  estimatedTotalPrice?: number | null
  actualTotalPrice?: number | null
  sourceType: string
  catalogItemId?: string | null
  catalogItemName?: string | null
  priceOptions: ShoppingItemPriceOption[]
  shoppingListId: string
}

type ShoppingList = {
  id: string
  name: string
  createdAt: string
  items: ShoppingItem[]
}

type ShoppingListStoreSummary = {
  storeName: string
  totalEstimatedPrice: number
  coveredItemsCount: number
  totalItemsCount: number
  isComplete: boolean
  isBestOption: boolean
}

const catalogItems = ref<CatalogItem[]>([])
const shoppingLists = ref<ShoppingList[]>([])
const storeSummaries = ref<ShoppingListStoreSummary[]>([])
const selectedListId = ref<string>('')
const loading = ref(false)
const error = ref('')
const success = ref('')

const newList = ref({
  name: '',
})

const editListId = ref<string | null>(null)
const editList = ref({
  name: '',
})

const newItem = ref({
  name: '',
  requiredQuantity: 1,
  inventoryQuantityUsed: 0,
  quantity: 1,
  purchasedQuantity: 0,
  unit: 'Stk',
  sourceType: 'Manual',
  catalogItemId: '' as string | '',
})

const editItemId = ref<string | null>(null)
const editItem = ref({
  purchasedQuantity: 0,
  actualTotalPrice: null as number | null,
  isChecked: false,
})

const newPriceOptionByItemId = ref<Record<string, {
  storeName: string
  productName: string
  unitPrice: number
  totalPrice: number
  productUrl: string
  isAvailable: boolean
}>>({})

const editPriceOptionId = ref<string | null>(null)
const editPriceOption = ref({
  storeName: '',
  productName: '',
  unitPrice: 0,
  totalPrice: 0,
  productUrl: '',
  isAvailable: true,
})

const selectedList = computed(() =>
  shoppingLists.value.find((x) => x.id === selectedListId.value) ?? null,
)

const selectedListTotals = computed(() => {
  const items = selectedList.value?.items ?? []

  const estimated = items.reduce((sum, item) => sum + (item.estimatedTotalPrice ?? 0), 0)
  const actual = items.reduce((sum, item) => sum + (item.actualTotalPrice ?? 0), 0)

  return {
    estimated,
    actual,
  }
})

function formatMoney(value?: number | null) {
  if (value == null) {
    return '—'
  }

  return `${value.toFixed(2)} €`
}

function displaySource(sourceType: string) {
  switch (sourceType) {
    case 'MealPlanWeek':
      return 'Wochenplan'
    case 'Recipe':
      return 'Rezept'
    case 'Manual':
      return 'Manuell'
    default:
      return sourceType
  }
}

function applyCatalogSelection(
  catalogItemId: string,
  target: {
    name: string
    unit: string
    sourceType: string
  },
) {
  const selectedCatalogItem = catalogItems.value.find((x) => x.id === catalogItemId)

  if (!selectedCatalogItem) {
    return
  }

  target.name = selectedCatalogItem.name
  target.unit = selectedCatalogItem.defaultUnit
  target.sourceType = 'Manual'
}

function getNewPriceOptionState(item: ShoppingItem) {
  if (!newPriceOptionByItemId.value[item.id]) {
    newPriceOptionByItemId.value[item.id] = {
      storeName: '',
      productName: item.catalogItemName || item.name,
      unitPrice: 0,
      totalPrice: 0,
      productUrl: '',
      isAvailable: true,
    }
  }

  return newPriceOptionByItemId.value[item.id]
}

async function loadStoreSummaries() {
  if (!selectedListId.value) {
    storeSummaries.value = []
    return
  }

  try {
    storeSummaries.value = await apiFetch<ShoppingListStoreSummary[]>(
      `/shoppinglists/${selectedListId.value}/store-summaries`,
    )
  } catch (err) {
    console.error(err)
    storeSummaries.value = []
  }
}

async function loadData() {
  loading.value = true
  error.value = ''
  success.value = ''

  try {
    const [loadedCatalogItems, loadedShoppingLists] = await Promise.all([
      apiFetch<CatalogItem[]>('/catalog'),
      apiFetch<ShoppingList[]>('/shoppinglists'),
    ])

    catalogItems.value = loadedCatalogItems
    shoppingLists.value = loadedShoppingLists

    if (!selectedListId.value && loadedShoppingLists.length > 0) {
      selectedListId.value = loadedShoppingLists[0].id
    }

    if (selectedListId.value && !loadedShoppingLists.some((x) => x.id === selectedListId.value)) {
      selectedListId.value = loadedShoppingLists[0]?.id ?? ''
    }

    if (selectedListId.value) {
      await loadStoreSummaries()
    } else {
      storeSummaries.value = []
    }
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Einkaufslisten konnten nicht geladen werden.'
  } finally {
    loading.value = false
  }
}

async function createList() {
  error.value = ''
  success.value = ''

  try {
    const created = await apiFetch<ShoppingList>('/shoppinglists', {
      method: 'POST',
      body: JSON.stringify(newList.value),
    })

    newList.value.name = ''
    await loadData()
    selectedListId.value = created.id
    success.value = 'Einkaufsliste wurde erstellt.'
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Einkaufsliste konnte nicht erstellt werden.'
  }
}

function startEditList(list: ShoppingList) {
  editListId.value = list.id
  editList.value.name = list.name
}

function cancelEditList() {
  editListId.value = null
  editList.value.name = ''
}

async function updateList(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<ShoppingList>(`/shoppinglists/${id}`, {
      method: 'PUT',
      body: JSON.stringify(editList.value),
    })

    cancelEditList()
    await loadData()
    success.value = 'Einkaufsliste wurde aktualisiert.'
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Einkaufsliste konnte nicht aktualisiert werden.'
  }
}

async function deleteList(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>(`/shoppinglists/${id}`, {
      method: 'DELETE',
    })

    if (editListId.value === id) {
      cancelEditList()
    }

    if (selectedListId.value === id) {
      selectedListId.value = ''
    }

    await loadData()
    success.value = 'Einkaufsliste wurde gelöscht.'
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Einkaufsliste konnte nicht gelöscht werden.'
  }
}

async function createItem() {
  if (!selectedListId.value) {
    error.value = 'Bitte zuerst eine Einkaufsliste auswählen.'
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items`, {
      method: 'POST',
      body: JSON.stringify({
  ...newItem.value,
  requiredQuantity: Number(newItem.value.requiredQuantity),
  inventoryQuantityUsed: Number(newItem.value.inventoryQuantityUsed),
  quantity: Number(newItem.value.quantity),
  purchasedQuantity: Number(newItem.value.purchasedQuantity),
  estimatedUnitPrice: null,
  estimatedTotalPrice: null,
  actualTotalPrice: null,
  catalogItemId: newItem.value.catalogItemId || null,
}),
    })

    newItem.value = {
  name: '',
  requiredQuantity: 1,
  inventoryQuantityUsed: 0,
  quantity: 1,
  purchasedQuantity: 0,
  unit: 'Stk',
  sourceType: 'Manual',
  catalogItemId: '',
}

    await loadData()
    success.value = 'Artikel wurde erstellt.'
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Artikel konnte nicht erstellt werden.'
  }
}

function startEditItem(item: ShoppingItem) {
  editItemId.value = item.id
  editItem.value = {
    purchasedQuantity: item.purchasedQuantity,
    actualTotalPrice: item.actualTotalPrice ?? null,
    isChecked: item.isChecked,
  }
}

function cancelEditItem() {
  editItemId.value = null
  editItem.value = {
    purchasedQuantity: 0,
    actualTotalPrice: null,
    isChecked: false,
  }
}

async function updateItem(item: ShoppingItem) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items/${item.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: item.name,
        requiredQuantity: item.requiredQuantity,
        inventoryQuantityUsed: item.inventoryQuantityUsed,
        quantity: item.quantity,
        purchasedQuantity: Number(editItem.value.purchasedQuantity),
        unit: item.unit,
        isChecked: editItem.value.isChecked,
        estimatedUnitPrice: item.estimatedUnitPrice ?? null,
        estimatedTotalPrice: item.estimatedTotalPrice ?? null,
        actualTotalPrice:
          editItem.value.actualTotalPrice == null ? null : Number(editItem.value.actualTotalPrice),
        sourceType: item.sourceType,
        catalogItemId: item.catalogItemId ?? null,
      }),
    })

    cancelEditItem()
    await loadData()
    success.value = 'Einkaufsdaten wurden aktualisiert.'
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Artikel konnte nicht aktualisiert werden.'
  }
}

async function toggleItem(item: ShoppingItem) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items/${item.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: item.name,
        requiredQuantity: item.requiredQuantity,
        inventoryQuantityUsed: item.inventoryQuantityUsed,
        quantity: item.quantity,
        purchasedQuantity: item.purchasedQuantity,
        unit: item.unit,
        isChecked: !item.isChecked,
        estimatedUnitPrice: item.estimatedUnitPrice ?? null,
        estimatedTotalPrice: item.estimatedTotalPrice ?? null,
        actualTotalPrice: item.actualTotalPrice ?? null,
        sourceType: item.sourceType,
        catalogItemId: item.catalogItemId ?? null,
      }),
    })

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Artikel konnte nicht aktualisiert werden.'
  }
}

async function deleteItem(itemId: string) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>(`/shoppinglists/${selectedListId.value}/items/${itemId}`, {
      method: 'DELETE',
    })

    if (editItemId.value === itemId) {
      cancelEditItem()
    }

    await loadData()
    success.value = 'Artikel wurde gelöscht.'
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Artikel konnte nicht gelöscht werden.'
  }
}

async function completeShoppingList(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch(`/shoppinglists/${id}/complete`, {
      method: 'POST',
    })

    success.value = 'Einkauf wurde mit den tatsächlich gekauften Mengen ins Inventar übernommen.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Fehler beim Abschließen der Einkaufsliste.'
  }
}

function startEditPriceOption(priceOption: ShoppingItemPriceOption) {
  editPriceOptionId.value = priceOption.id
  editPriceOption.value = {
    storeName: priceOption.storeName,
    productName: priceOption.productName,
    unitPrice: priceOption.unitPrice,
    totalPrice: priceOption.totalPrice,
    productUrl: priceOption.productUrl ?? '',
    isAvailable: priceOption.isAvailable,
  }
}

function cancelEditPriceOption() {
  editPriceOptionId.value = null
  editPriceOption.value = {
    storeName: '',
    productName: '',
    unitPrice: 0,
    totalPrice: 0,
    productUrl: '',
    isAvailable: true,
  }
}

async function createPriceOption(item: ShoppingItem) {
  if (!selectedListId.value) {
    return
  }

  const state = getNewPriceOptionState(item)

  error.value = ''
  success.value = ''

  try {
    await apiFetch<ShoppingItemPriceOption>(
      `/shoppinglists/${selectedListId.value}/items/${item.id}/price-options`,
      {
        method: 'POST',
        body: JSON.stringify({
          storeName: state.storeName,
          productName: state.productName,
          unitPrice: Number(state.unitPrice),
          totalPrice: Number(state.totalPrice),
          productUrl: state.productUrl || null,
          isAvailable: state.isAvailable,
        }),
      },
    )

    newPriceOptionByItemId.value[item.id] = {
      storeName: '',
      productName: item.catalogItemName || item.name,
      unitPrice: 0,
      totalPrice: 0,
      productUrl: '',
      isAvailable: true,
    }

    success.value = 'Preisoption wurde hinzugefügt.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Preisoption konnte nicht erstellt werden.'
  }
}

async function updatePriceOption(item: ShoppingItem, priceOptionId: string) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<ShoppingItemPriceOption>(
      `/shoppinglists/${selectedListId.value}/items/${item.id}/price-options/${priceOptionId}`,
      {
        method: 'PUT',
        body: JSON.stringify({
          storeName: editPriceOption.value.storeName,
          productName: editPriceOption.value.productName,
          unitPrice: Number(editPriceOption.value.unitPrice),
          totalPrice: Number(editPriceOption.value.totalPrice),
          productUrl: editPriceOption.value.productUrl || null,
          isAvailable: editPriceOption.value.isAvailable,
        }),
      },
    )

    cancelEditPriceOption()
    success.value = 'Preisoption wurde aktualisiert.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Preisoption konnte nicht aktualisiert werden.'
  }
}

async function deletePriceOption(item: ShoppingItem, priceOptionId: string) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>(
      `/shoppinglists/${selectedListId.value}/items/${item.id}/price-options/${priceOptionId}`,
      {
        method: 'DELETE',
      },
    )

    if (editPriceOptionId.value === priceOptionId) {
      cancelEditPriceOption()
    }

    success.value = 'Preisoption wurde gelöscht.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Preisoption konnte nicht gelöscht werden.'
  }
}

onMounted(loadData)
</script>

<template>
  <section>
    <h2>Einkaufsliste</h2>

    <p v-if="loading">Lade Daten...</p>
    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="success" class="success">{{ success }}</p>

    <div class="grid">
      <div class="card">
        <h3>Neue Einkaufsliste</h3>

        <form class="form" @submit.prevent="createList">
          <input v-model="newList.name" type="text" placeholder="Name der Liste" required />
          <button type="submit">Speichern</button>
        </form>
      </div>

      <div class="card">
        <h3>Artikel hinzufügen</h3>

        <form class="form" @submit.prevent="createItem">
          <select
            v-model="newItem.catalogItemId"
            @change="applyCatalogSelection(newItem.catalogItemId, newItem)"
          >
            <option value="">Ohne Katalogeintrag</option>
            <option v-for="catalogItem in catalogItems" :key="catalogItem.id" :value="catalogItem.id">
              {{ catalogItem.name }} ({{ catalogItem.defaultUnit }})
            </option>
          </select>

          <input v-model="newItem.name" type="text" placeholder="Artikelname" required />
          <input
            v-model="newItem.requiredQuantity"
            type="number"
            step="0.01"
            placeholder="Benötigt"
            required
          />
          <input
            v-model="newItem.inventoryQuantityUsed"
            type="number"
            step="0.01"
            placeholder="Aus Inventar gedeckt"
          />
          <input
            v-model="newItem.quantity"
            type="number"
            step="0.01"
            placeholder="Zu kaufen"
            required
          />
          <input
            v-model="newItem.unit"
            type="text"
            placeholder="Einheit"
            required
          />

          <button type="submit" :disabled="!selectedListId">Speichern</button>
        </form>
      </div>
    </div>

    <div class="card">
      <h3>Listen</h3>

      <table v-if="shoppingLists.length > 0">
        <thead>
          <tr>
            <th>Name</th>
            <th>Erstellt</th>
            <th>Artikel</th>
            <th>Geschätzt</th>
            <th>Tatsächlich</th>
            <th>Aktionen</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="list in shoppingLists"
            :key="list.id"
            :class="{ selected: selectedListId === list.id }"
          >
            <template v-if="editListId === list.id">
              <td>
                <input v-model="editList.name" type="text" />
              </td>
              <td>{{ new Date(list.createdAt).toLocaleString() }}</td>
              <td>{{ list.items.length }}</td>
              <td>
                {{ formatMoney(list.items.reduce((sum, item) => sum + (item.estimatedTotalPrice ?? 0), 0)) }}
              </td>
              <td>
                {{ formatMoney(list.items.reduce((sum, item) => sum + (item.actualTotalPrice ?? 0), 0)) }}
              </td>
              <td class="actions">
                <button @click="updateList(list.id)">Speichern</button>
                <button @click="cancelEditList">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td @click="selectedListId = list.id" class="clickable">{{ list.name }}</td>
              <td>{{ new Date(list.createdAt).toLocaleString() }}</td>
              <td>{{ list.items.length }}</td>
              <td>
                {{ formatMoney(list.items.reduce((sum, item) => sum + (item.estimatedTotalPrice ?? 0), 0)) }}
              </td>
              <td>
                {{ formatMoney(list.items.reduce((sum, item) => sum + (item.actualTotalPrice ?? 0), 0)) }}
              </td>
              <td class="actions">
                <button @click="selectedListId = list.id">Öffnen</button>
                <button @click="startEditList(list)">Bearbeiten</button>
                <button @click="deleteList(list.id)">Löschen</button>
                <button @click="completeShoppingList(list.id)">Einkauf abschließen</button>
              </td>
            </template>
          </tr>
        </tbody>
      </table>

      <p v-else>Noch keine Einkaufslisten vorhanden.</p>
    </div>

    <div class="card" v-if="selectedList">
      <h3>Artikel in „{{ selectedList.name }}“</h3>
      <p class="hint">
        Bedarf, Inventarabzug, zu kaufende Menge und Preisvorschläge sind automatisch.
        Manuell pflegst du hier nur den echten Einkauf.
      </p>

      <div class="totals">
        <div class="total-box">
          <span>Geschätzte Kosten</span>
          <strong>{{ formatMoney(selectedListTotals.estimated) }}</strong>
        </div>
        <div class="total-box">
          <span>Tatsächliche Kosten</span>
          <strong>{{ formatMoney(selectedListTotals.actual) }}</strong>
        </div>
      </div>

      <div class="store-summary-card">
        <h4>Händlervergleich</h4>

        <table v-if="storeSummaries.length > 0">
          <thead>
            <tr>
              <th>Händler</th>
              <th>Gesamtsumme</th>
              <th>Abdeckung</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="summary in storeSummaries"
              :key="summary.storeName"
              :class="{ best: summary.isBestOption }"
            >
              <td>
                {{ summary.storeName }}
                <span v-if="summary.isBestOption" class="best-badge">Beste Option</span>
              </td>
              <td>{{ formatMoney(summary.totalEstimatedPrice) }}</td>
              <td>{{ summary.coveredItemsCount }} / {{ summary.totalItemsCount }}</td>
              <td>
                <span v-if="summary.isComplete" class="complete">vollständig</span>
                <span v-else class="partial">teilweise</span>
              </td>
            </tr>
          </tbody>
        </table>

        <p v-else>Noch keine Händlerpreise für diese Einkaufsliste vorhanden.</p>
      </div>

      <table v-if="selectedList.items.length > 0">
        <thead>
          <tr>
            <th>Erledigt</th>
            <th>Name</th>
            <th>Katalog</th>
            <th>Quelle</th>
            <th>Benötigt</th>
            <th>Inventar</th>
            <th>Zu kaufen</th>
            <th>Gekauft</th>
            <th>Einheit</th>
            <th>Geschätzt</th>
            <th>Tatsächlich</th>
            <th>Aktionen</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="item in selectedList.items" :key="item.id">
            <tr>
              <template v-if="editItemId === item.id">
                <td>
                  <input v-model="editItem.isChecked" type="checkbox" />
                </td>
                <td>{{ item.name }}</td>
                <td>{{ item.catalogItemName || '—' }}</td>
                <td>{{ displaySource(item.sourceType) }}</td>
                <td>{{ item.requiredQuantity }}</td>
                <td>{{ item.inventoryQuantityUsed }}</td>
                <td class="to-buy">{{ item.quantity }}</td>
                <td>
                  <input v-model="editItem.purchasedQuantity" type="number" step="0.01" />
                </td>
                <td>{{ item.unit }}</td>
                <td>{{ formatMoney(item.estimatedTotalPrice) }}</td>
                <td>
                  <input v-model="editItem.actualTotalPrice" type="number" step="0.01" />
                </td>
                <td class="actions">
                  <button @click="updateItem(item)">Speichern</button>
                  <button @click="cancelEditItem">Abbrechen</button>
                </td>
              </template>

              <template v-else>
                <td>
                  <input :checked="item.isChecked" type="checkbox" @change="toggleItem(item)" />
                </td>
                <td :class="{ checked: item.isChecked }">{{ item.name }}</td>
                <td>{{ item.catalogItemName || '—' }}</td>
                <td>{{ displaySource(item.sourceType) }}</td>
                <td>{{ item.requiredQuantity }}</td>
                <td>{{ item.inventoryQuantityUsed }}</td>
                <td class="to-buy">{{ item.quantity }}</td>
                <td>{{ item.purchasedQuantity > 0 ? item.purchasedQuantity : '—' }}</td>
                <td>{{ item.unit }}</td>
                <td>{{ formatMoney(item.estimatedTotalPrice) }}</td>
                <td>{{ formatMoney(item.actualTotalPrice) }}</td>
                <td class="actions">
                  <button @click="startEditItem(item)">Echten Einkauf pflegen</button>
                  <button @click="deleteItem(item.id)">Löschen</button>
                </td>
              </template>
            </tr>

            <tr class="price-options-row">
              <td colspan="12">
                <div class="price-options-wrapper">
                  <h4>Preisoptionen für {{ item.name }}</h4>

                  <table v-if="item.priceOptions.length > 0" class="nested-table">
                    <thead>
                      <tr>
                        <th>Händler</th>
                        <th>Produkt</th>
                        <th>Einzelpreis</th>
                        <th>Gesamtpreis</th>
                        <th>Verfügbar</th>
                        <th>Link</th>
                        <th>Aktionen</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="priceOption in item.priceOptions" :key="priceOption.id">
                        <template v-if="editPriceOptionId === priceOption.id">
                          <td>
                            <input v-model="editPriceOption.storeName" type="text" />
                          </td>
                          <td>
                            <input v-model="editPriceOption.productName" type="text" />
                          </td>
                          <td>
                            <input v-model="editPriceOption.unitPrice" type="number" step="0.01" />
                          </td>
                          <td>
                            <input v-model="editPriceOption.totalPrice" type="number" step="0.01" />
                          </td>
                          <td>
                            <input v-model="editPriceOption.isAvailable" type="checkbox" />
                          </td>
                          <td>
                            <input v-model="editPriceOption.productUrl" type="text" />
                          </td>
                          <td class="actions">
                            <button @click="updatePriceOption(item, priceOption.id)">Speichern</button>
                            <button @click="cancelEditPriceOption">Abbrechen</button>
                          </td>
                        </template>

                        <template v-else>
                          <td>{{ priceOption.storeName }}</td>
                          <td>{{ priceOption.productName }}</td>
                          <td>{{ formatMoney(priceOption.unitPrice) }}</td>
                          <td>{{ formatMoney(priceOption.totalPrice) }}</td>
                          <td>{{ priceOption.isAvailable ? 'Ja' : 'Nein' }}</td>
                          <td>
                            <a
                              v-if="priceOption.productUrl"
                              :href="priceOption.productUrl"
                              target="_blank"
                              rel="noreferrer"
                            >
                              öffnen
                            </a>
                            <span v-else>—</span>
                          </td>
                          <td class="actions">
                            <button @click="startEditPriceOption(priceOption)">Bearbeiten</button>
                            <button @click="deletePriceOption(item, priceOption.id)">Löschen</button>
                          </td>
                        </template>
                      </tr>
                    </tbody>
                  </table>

                  <p v-else class="no-price-options">Noch keine Preisoptionen vorhanden.</p>

                  <div class="price-option-form">
                    <h5>Preisoption hinzufügen</h5>

                    <div class="price-option-grid">
                      <input
                        v-model="getNewPriceOptionState(item).storeName"
                        type="text"
                        placeholder="Händler, z. B. Spar"
                      />
                      <input
                        v-model="getNewPriceOptionState(item).productName"
                        type="text"
                        placeholder="Produktname"
                      />
                      <input
                        v-model="getNewPriceOptionState(item).unitPrice"
                        type="number"
                        step="0.01"
                        placeholder="Einzelpreis"
                      />
                      <input
                        v-model="getNewPriceOptionState(item).totalPrice"
                        type="number"
                        step="0.01"
                        placeholder="Gesamtpreis"
                      />
                      <input
                        v-model="getNewPriceOptionState(item).productUrl"
                        type="text"
                        placeholder="Produktlink"
                      />
                      <label class="checkbox-row">
                        <input v-model="getNewPriceOptionState(item).isAvailable" type="checkbox" />
                        Verfügbar
                      </label>
                    </div>

                    <button @click="createPriceOption(item)">Preisoption speichern</button>
                  </div>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>

      <p v-else>Diese Einkaufsliste enthält noch keine Artikel.</p>
    </div>
  </section>
</template>

<style scoped>
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

.form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

input,
button,
select {
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

.error {
  color: #b00020;
  margin-bottom: 16px;
}

.success {
  color: #0a7a2f;
  margin-bottom: 16px;
}

.selected {
  background-color: #f6f6f6;
}

.clickable {
  cursor: pointer;
}

.checked {
  text-decoration: line-through;
  opacity: 0.7;
}

.hint {
  margin-bottom: 12px;
  color: #555;
  font-size: 0.95rem;
}

.totals {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 16px;
}

.total-box {
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 12px 16px;
  min-width: 220px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.to-buy {
  font-weight: 700;
}

.store-summary-card {
  margin-bottom: 16px;
}

.best {
  background-color: #f2f8f2;
}

.best-badge {
  margin-left: 8px;
  color: #0a7a2f;
  font-weight: 600;
}

.complete {
  color: #0a7a2f;
  font-weight: 600;
}

.partial {
  color: #b06a00;
  font-weight: 600;
}

.price-options-row td {
  background: #fafafa;
}

.price-options-wrapper {
  padding: 8px 0;
}

.nested-table {
  margin-top: 8px;
  margin-bottom: 12px;
}

.no-price-options {
  margin-top: 8px;
  margin-bottom: 12px;
  color: #666;
}

.price-option-form {
  border: 1px dashed #ccc;
  border-radius: 8px;
  padding: 12px;
  margin-top: 8px;
}

.price-option-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(180px, 1fr));
  gap: 10px;
  margin-bottom: 12px;
}

.checkbox-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

@media (max-width: 1100px) {
  .price-option-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 900px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
