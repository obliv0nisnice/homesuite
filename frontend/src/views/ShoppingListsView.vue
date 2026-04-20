<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '@/services/api'

type Category = {
  id: string
  name: string
  type: string
}

const categories = ref<Category[]>([])
const completeAsBudgetExpense = ref<Record<string, boolean>>({})
const selectedExpenseCategoryId = ref<Record<string, string>>({})

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

const expenseCategories = computed(() =>
  categories.value.filter((c) => c.type === 'Expense'),
)

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

  return newPriceOptionByItemId.value[item.id]!
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
    const [loadedCatalogItems, loadedShoppingLists, loadedCategories] = await Promise.all([
      apiFetch<CatalogItem[]>('/catalog'),
      apiFetch<ShoppingList[]>('/shoppinglists'),
      apiFetch<Category[]>('/categories'),
    ])

    catalogItems.value = loadedCatalogItems
    shoppingLists.value = loadedShoppingLists
    categories.value = loadedCategories

    for (const list of loadedShoppingLists) {
      if (!selectedExpenseCategoryId.value[list.id]) {
        selectedExpenseCategoryId.value[list.id] =
          loadedCategories.find((c) => c.type === 'Expense')?.id ?? ''
      }
    }

    if (!selectedListId.value && loadedShoppingLists.length > 0) {
      selectedListId.value = loadedShoppingLists[0]?.id ?? ''
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

    delete completeAsBudgetExpense.value[id]
    delete selectedExpenseCategoryId.value[id]

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

  const createBudgetExpense = completeAsBudgetExpense.value[id] ?? false
  const expenseCategoryId = selectedExpenseCategoryId.value[id] ?? ''

  if (createBudgetExpense && !expenseCategoryId) {
    error.value = 'Bitte eine Budget-Kategorie auswählen.'
    return
  }

  const list = shoppingLists.value.find((x) => x.id === id)

  try {
    await apiFetch(`/shoppinglists/${id}/complete`, {
      method: 'POST',
      body: JSON.stringify({
        createBudgetExpense,
        expenseCategoryId: createBudgetExpense ? expenseCategoryId : null,
        transactionTitle: list ? `Einkauf · ${list.name}` : 'Einkauf',
        transactionDate: new Date().toISOString().slice(0, 10),
      }),
    })

    success.value = createBudgetExpense
      ? 'Einkauf wurde ins Inventar übernommen und zusätzlich als Budget-Ausgabe gespeichert.'
      : 'Einkauf wurde mit den tatsächlich gekauften Mengen ins Inventar übernommen.'

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
  <div class="dashboard-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Einkaufsliste <span class="title-accent">Marktzettel</span></h1>
        <p class="page-subtitle">Charmant wie ein echter Einkaufszettel, aber mit Preisen, Quellen und Inventar-Abgleich.</p>
      </div>
    </div>

    <div v-if="error" class="alert alert-error">{{ error }}</div>
    <div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="alert">Lade Einkaufslisten…</div>

    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-icon">🧾</div>
        <div class="stat-info">
          <span class="stat-label">Listen</span>
          <span class="stat-value">{{ shoppingLists.length }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">🛍️</div>
        <div class="stat-info">
          <span class="stat-label">Ausgewählte Liste</span>
          <span class="stat-value">{{ selectedList?.items.length ?? 0 }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">💶</div>
        <div class="stat-info">
          <span class="stat-label">Geschätzt</span>
          <span class="stat-value">{{ formatMoney(selectedListTotals.estimated) }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">🧮</div>
        <div class="stat-info">
          <span class="stat-label">Tatsächlich</span>
          <span class="stat-value">{{ formatMoney(selectedListTotals.actual) }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
    </div>

    <div class="content-grid">
      <div class="stack">
        <div class="form-card">
          <div class="card-header">
            <div>
              <h2 class="card-title">Neue Einkaufsliste</h2>
              <p class="card-copy">Lege eine neue Liste an und öffne sie direkt für Artikel und Preisvorschläge.</p>
            </div>
          </div>

          <form @submit.prevent="createList">
            <div class="form-grid full">
              <input v-model="newList.name" type="text" placeholder="Name der Liste" required />
            </div>
            <div class="form-actions">
              <button class="btn-add" type="submit">Liste anlegen</button>
            </div>
          </form>
        </div>

        <div class="data-card">
          <div class="card-header">
            <div>
              <h2 class="card-title">Listenübersicht</h2>
              <p class="card-copy">Eine Liste öffnen, um Artikel und Preisoptionen zu sehen.</p>
            </div>
          </div>

          <table v-if="shoppingLists.length > 0" class="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Erstellt</th>
                <th>Artikel</th>
                <th>Geschätzt</th>
                <th>Tatsächlich</th>
                <th>Budget</th>
                <th>Aktionen</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="list in shoppingLists" :key="list.id">
                <tr :class="{ selected: selectedListId === list.id }">
                  <template v-if="editListId === list.id">
                    <td><input v-model="editList.name" type="text" /></td>
                    <td>{{ new Date(list.createdAt).toLocaleString() }}</td>
                    <td>{{ list.items.length }}</td>
                    <td>{{ formatMoney(list.items.reduce((sum, item) => sum + (item.estimatedTotalPrice ?? 0), 0)) }}</td>
                    <td>{{ formatMoney(list.items.reduce((sum, item) => sum + (item.actualTotalPrice ?? 0), 0)) }}</td>
                    <td>—</td>
                    <td class="actions">
                      <button class="btn-save" @click="updateList(list.id)">Speichern</button>
                      <button class="btn-secondary" @click="cancelEditList">Abbrechen</button>
                    </td>
                  </template>
                  <template v-else>
                    <td class="clickable" @click="selectedListId = list.id"><strong>{{ list.name }}</strong></td>
                    <td>{{ new Date(list.createdAt).toLocaleString() }}</td>
                    <td>{{ list.items.length }}</td>
                    <td>{{ formatMoney(list.items.reduce((sum, item) => sum + (item.estimatedTotalPrice ?? 0), 0)) }}</td>
                    <td>{{ formatMoney(list.items.reduce((sum, item) => sum + (item.actualTotalPrice ?? 0), 0)) }}</td>
                    <td>
                      <div class="complete-budget-box">
                        <label class="checkbox-row">
                          <input v-model="completeAsBudgetExpense[list.id]" type="checkbox" />
                          <span>Als Budget-Ausgabe speichern</span>
                        </label>

                        <select
                          v-if="completeAsBudgetExpense[list.id]"
                          v-model="selectedExpenseCategoryId[list.id]"
                          class="budget-category-select"
                        >
                          <option value="">Budget-Kategorie wählen</option>
                          <option v-for="category in expenseCategories" :key="category.id" :value="category.id">
                            {{ category.name }}
                          </option>
                        </select>
                      </div>
                    </td>
                    <td class="actions">
                      <button class="btn-secondary" @click="selectedListId = list.id">Öffnen</button>
                      <button class="btn-secondary" @click="startEditList(list)">Bearbeiten</button>
                      <button class="btn-danger" @click="deleteList(list.id)">Löschen</button>
                      <button class="btn-add" @click="completeShoppingList(list.id)">Abschließen</button>
                    </td>
                  </template>
                </tr>
              </template>
            </tbody>
          </table>

          <div v-else class="empty-state">Noch keine Einkaufslisten vorhanden.</div>
        </div>

        <div v-if="selectedList" class="form-card">
          <div class="card-header">
            <div>
              <h2 class="card-title">Artikel zu „{{ selectedList.name }}“ hinzufügen</h2>
              <p class="card-copy">Die Liste bleibt charmant manuell, bekommt aber automatisch Inventar- und Preisdaten dazu.</p>
            </div>
          </div>

          <form @submit.prevent="createItem">
            <div class="form-grid">
              <select v-model="newItem.catalogItemId" @change="applyCatalogSelection(newItem.catalogItemId, newItem)">
                <option value="">Katalogartikel optional wählen</option>
                <option v-for="catalogItem in catalogItems" :key="catalogItem.id" :value="catalogItem.id">
                  {{ catalogItem.name }}
                </option>
              </select>
              <input v-model="newItem.name" type="text" placeholder="Name" required />
              <input v-model="newItem.requiredQuantity" type="number" min="0" step="0.01" placeholder="Benötigt" required />
              <input v-model="newItem.unit" type="text" placeholder="Einheit" required />
            </div>
            <div class="form-actions">
              <button class="btn-add" type="submit">Artikel hinzufügen</button>
            </div>
          </form>
        </div>
      </div>

      <div v-if="selectedList" class="sheet-card grocery-sheet">
        <div class="sheet-header">
          <div>
            <h2 class="card-title">„{{ selectedList.name }}“</h2>
            <p class="card-copy">Bedarf, Inventar-Abzug und Preisoptionen werden automatisch gerechnet. Den echten Einkauf pflegst du hier mit Charme nach.</p>
          </div>
        </div>

        <div class="totals-strip">
          <div class="note-pill"><span>Geschätzt</span><strong>{{ formatMoney(selectedListTotals.estimated) }}</strong></div>
          <div class="note-pill"><span>Tatsächlich</span><strong>{{ formatMoney(selectedListTotals.actual) }}</strong></div>
        </div>

        <div class="store-compare" v-if="storeSummaries.length > 0">
          <div class="card-header">
            <h3 class="card-title">Händlervergleich</h3>
          </div>
          <div class="compact-list">
            <div v-for="summary in storeSummaries" :key="summary.storeName" class="compact-item">
              <div class="card-header" style="margin-bottom:8px;">
                <strong>{{ summary.storeName }}</strong>
                <span :class="['badge', summary.isBestOption ? 'badge-success' : summary.isComplete ? 'badge-primary' : 'badge-warning']">
                  {{ summary.isBestOption ? 'Beste Option' : summary.isComplete ? 'vollständig' : 'teilweise' }}
                </span>
              </div>
              <div class="card-copy">{{ summary.coveredItemsCount }} / {{ summary.totalItemsCount }} Artikel · {{ formatMoney(summary.totalEstimatedPrice) }}</div>
            </div>
          </div>
        </div>

        <div class="grocery-list">
          <div v-if="selectedList.items.length === 0" class="empty-state">Noch keine Artikel auf dieser Einkaufsliste.</div>

          <div v-for="item in selectedList.items" :key="item.id" class="grocery-row" :class="{ checked: item.isChecked }">
            <div class="grocery-main">
              <label class="inline-checkbox">
                <input :checked="item.isChecked" type="checkbox" @change="toggleItem(item)" />
              </label>

              <div class="grocery-text">
                <div class="grocery-name">{{ item.name }}</div>
                <div class="grocery-meta">
                  <span>{{ displaySource(item.sourceType) }}</span>
                  <span v-if="item.catalogItemName">· {{ item.catalogItemName }}</span>
                  <span>· {{ item.requiredQuantity }} {{ item.unit }} Bedarf</span>
                  <span>· {{ item.inventoryQuantityUsed }} aus Inventar</span>
                </div>
                <div class="grocery-buyline">
                  Zu kaufen: <strong>{{ item.quantity }} {{ item.unit }}</strong>
                  <span class="price-inline">Geschätzt {{ formatMoney(item.estimatedTotalPrice) }}</span>
                  <span class="price-inline">Echt {{ formatMoney(item.actualTotalPrice) }}</span>
                </div>
              </div>

              <div class="actions">
                <button class="btn-secondary" @click="startEditItem(item)">Echten Einkauf pflegen</button>
                <button class="btn-danger" @click="deleteItem(item.id)">Löschen</button>
              </div>
            </div>

            <div v-if="editItemId === item.id" class="inline-editor">
              <div class="form-grid">
                <input v-model="editItem.purchasedQuantity" type="number" step="0.01" placeholder="Gekauft" />
                <input v-model="editItem.actualTotalPrice" type="number" step="0.01" placeholder="Tatsächlicher Preis" />
              </div>
              <div class="form-actions">
                <label class="checkbox-row"><input v-model="editItem.isChecked" type="checkbox" /> Bereits erledigt</label>
                <button class="btn-save" @click="updateItem(item)">Speichern</button>
                <button class="btn-secondary" @click="cancelEditItem">Abbrechen</button>
              </div>
            </div>

            <div class="price-board">
              <div class="card-header">
                <h3 class="card-title" style="font-size:16px;">Preisoptionen</h3>
              </div>

              <table v-if="item.priceOptions.length > 0" class="paper-table">
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
                      <td><input v-model="editPriceOption.storeName" type="text" /></td>
                      <td><input v-model="editPriceOption.productName" type="text" /></td>
                      <td><input v-model="editPriceOption.unitPrice" type="number" step="0.01" /></td>
                      <td><input v-model="editPriceOption.totalPrice" type="number" step="0.01" /></td>
                      <td><input v-model="editPriceOption.isAvailable" type="checkbox" /></td>
                      <td><input v-model="editPriceOption.productUrl" type="text" /></td>
                      <td class="actions">
                        <button class="btn-save" @click="updatePriceOption(item, priceOption.id)">Speichern</button>
                        <button class="btn-secondary" @click="cancelEditPriceOption">Abbrechen</button>
                      </td>
                    </template>
                    <template v-else>
                      <td>{{ priceOption.storeName }}</td>
                      <td>{{ priceOption.productName }}</td>
                      <td>{{ formatMoney(priceOption.unitPrice) }}</td>
                      <td>{{ formatMoney(priceOption.totalPrice) }}</td>
                      <td>{{ priceOption.isAvailable ? 'Ja' : 'Nein' }}</td>
                      <td>
                        <a v-if="priceOption.productUrl" :href="priceOption.productUrl" target="_blank" rel="noreferrer">öffnen</a>
                        <span v-else>—</span>
                      </td>
                      <td class="actions">
                        <button class="btn-secondary" @click="startEditPriceOption(priceOption)">Bearbeiten</button>
                        <button class="btn-danger" @click="deletePriceOption(item, priceOption.id)">Löschen</button>
                      </td>
                    </template>
                  </tr>
                </tbody>
              </table>

              <div v-else class="empty-state">Noch keine Preisoptionen vorhanden.</div>

              <div class="inline-editor" style="margin-top: 12px;">
                <div class="card-copy" style="margin-bottom:8px;">Preisoption hinzufügen</div>
                <div class="form-grid">
                  <input v-model="getNewPriceOptionState(item).storeName" type="text" placeholder="Händler" />
                  <input v-model="getNewPriceOptionState(item).productName" type="text" placeholder="Produktname" />
                  <input v-model="getNewPriceOptionState(item).unitPrice" type="number" step="0.01" placeholder="Einzelpreis" />
                  <input v-model="getNewPriceOptionState(item).totalPrice" type="number" step="0.01" placeholder="Gesamtpreis" />
                  <input v-model="getNewPriceOptionState(item).productUrl" class="field-span-2" type="text" placeholder="Produkt-URL" />
                </div>
                <div class="form-actions">
                  <label class="checkbox-row">
                    <input v-model="getNewPriceOptionState(item).isAvailable" type="checkbox" />
                    Verfügbar
                  </label>
                  <button class="btn-add" @click="createPriceOption(item)">Preisoption speichern</button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-else class="sheet-card">
        <div class="empty-state">Wähle links eine Einkaufsliste aus, um den Einkaufszettel zu sehen.</div>
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

.grocery-sheet {
  background:
    linear-gradient(to bottom, rgba(99,102,241,0.03), rgba(99,102,241,0.01)),
    repeating-linear-gradient(to bottom, transparent 0, transparent 35px, rgba(59,130,246,0.08) 36px);
  border: 1px solid rgba(99,102,241,0.16);
}

.sheet-header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}

.totals-strip {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  margin-bottom: 18px;
}

.note-pill {
  background: rgba(255,255,255,0.8);
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 10px 14px;
  display: inline-flex;
  gap: 10px;
  align-items: center;
}

.note-pill span { color: var(--text-muted); font-size: 13px; }
.note-pill strong { color: var(--text); }

.grocery-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.grocery-row {
  background: rgba(255,255,255,0.76);
  border: 1px solid rgba(99,102,241,0.12);
  border-radius: 18px;
  padding: 16px;
}

.grocery-row.checked {
  opacity: 0.72;
}

.grocery-main {
  display: grid;
  grid-template-columns: auto 1fr auto;
  gap: 14px;
  align-items: flex-start;
}
@media (max-width: 860px) {
  .grocery-main { grid-template-columns: 1fr; }
}

.grocery-name {
  font-size: 19px;
  font-weight: 800;
  color: var(--text);
  margin-bottom: 4px;
}

.grocery-meta,
.grocery-buyline {
  color: var(--text-muted);
  font-size: 14px;
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.price-inline {
  display: inline-flex;
  padding-left: 8px;
}

.inline-editor,
.price-board,
.store-compare {
  margin-top: 14px;
  background: rgba(255,255,255,0.72);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 14px;
}

.complete-budget-box {
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-width: 220px;
}

.budget-category-select {
  min-width: 220px;
}
</style>
