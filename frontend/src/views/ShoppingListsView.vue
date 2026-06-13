<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { apiFetch } from '@/services/api'
import { readOfflineCache, saveOfflineCache } from '@/services/offlineCache'
import {
  enqueueOfflineMutation,
  makeTempId,
  readOfflineMutations,
  writeOfflineMutations,
} from '@/services/offlineMutations'

type Category = {
  id: string
  name: string
  type: string
  monthlyLimit?: number | null
}

const categories = ref<Category[]>([])
const completeAsBudgetExpense = ref<Record<string, boolean>>({})
const selectedExpenseCategoryId = ref<Record<string, string>>({})

type BudgetTransaction = {
  amount: number
  date: string
  categoryId: string
  categoryType: string
}

const budgetTransactions = ref<BudgetTransaction[]>([])

type CatalogItem = {
  id: string
  name: string
  defaultUnit: string
  category?: string | null
  searchTerm?: string | null
  brandHint?: string | null
  isStaple: boolean
  prices?: Array<{
    id: string
    storeName: string
    productName: string
    unitPrice?: number | null
    totalPrice?: number | null
    productUrl?: string | null
    isAvailable: boolean
    checkedAt: string
    sourceType: string
  }>
  bestUnitPrice?: number | null
  bestTotalPrice?: number | null
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

type ShoppingItemPayload = {
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
}

type ShoppingListPayload = {
  name: string
}

type PriceOptionPayload = {
  storeName: string
  productName: string
  unitPrice: number
  totalPrice: number
  productUrl?: string | null
  isAvailable: boolean
}

type CompleteListPayload = {
  createBudgetExpense: boolean
  expenseCategoryId: string | null
  transactionTitle: string
  transactionDate: string
}

type ShoppingMutation =
  | {
      type: 'createList'
      shoppingListId: string
      payload: ShoppingListPayload
    }
  | {
      type: 'updateList'
      shoppingListId: string
      payload: ShoppingListPayload
    }
  | {
      type: 'deleteList'
      shoppingListId: string
    }
  | {
      type: 'completeList'
      shoppingListId: string
      payload: CompleteListPayload
    }
  | {
      type: 'createItem'
      shoppingListId: string
      itemId: string
      payload: ShoppingItemPayload
    }
  | {
      type: 'updateItem'
      shoppingListId: string
      itemId: string
      payload: ShoppingItemPayload
    }
  | {
      type: 'deleteItem'
      shoppingListId: string
      itemId: string
    }
  | {
      type: 'createPriceOption'
      shoppingListId: string
      itemId: string
      priceOptionId: string
      payload: PriceOptionPayload
    }
  | {
      type: 'updatePriceOption'
      shoppingListId: string
      itemId: string
      priceOptionId: string
      payload: PriceOptionPayload
    }
  | {
      type: 'deletePriceOption'
      shoppingListId: string
      itemId: string
      priceOptionId: string
    }

const catalogItems = ref<CatalogItem[]>([])
const shoppingLists = ref<ShoppingList[]>([])
const storeSummaries = ref<ShoppingListStoreSummary[]>([])
const selectedListId = ref<string>('')
const loading = ref(false)
const error = ref('')
const success = ref('')
const offlineSnapshotAt = ref<string | null>(null)
const pendingMutationCount = ref(0)
const isOnline = ref(typeof navigator === 'undefined' ? true : navigator.onLine)
const isSyncing = ref(false)

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

// Notizblock-Eingabe: eine Zeile = ein Artikel (Tastatur oder Apple-Pencil/iPadOS-Scribble).
const newLineText = ref('')
// Zeile → Katalog wird aktuell lokal/clientseitig zugeordnet; Umstellung vorgesehen.
const matchingMode = ref<'client' | 'llm'>('client')
const MATCH_THRESHOLD = 60

const editItemId = ref<string | null>(null)
const editItem = ref({
  purchasedQuantity: 0,
  actualTotalPrice: null as number | null,
  isChecked: false,
})

// Preisoptionen pro Artikel eingeklappt — der Zettel bleibt dadurch übersichtlich.
const expandedPriceItemIds = ref<Set<string>>(new Set())

function togglePriceOptions(itemId: string) {
  const expanded = new Set(expandedPriceItemIds.value)
  if (expanded.has(itemId)) {
    expanded.delete(itemId)
  } else {
    expanded.add(itemId)
  }
  expandedPriceItemIds.value = expanded
}

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

const bestCompleteStoreSummary = computed(() =>
  storeSummaries.value.find((summary) => summary.isBestOption && summary.isComplete) ?? null,
)

const expenseCategories = computed(() =>
  categories.value.filter((c) => c.type === 'Expense'),
)

// Default-Budgetkategorie: „Essen“ bevorzugt, sonst erste Expense-Kategorie.
function pickDefaultExpenseCategoryId(source: Category[]): string {
  const expense = source.filter((c) => c.type === 'Expense')
  const food = expense.find((c) => c.name.trim().toLowerCase() === 'essen')
  return (food ?? expense[0])?.id ?? ''
}

const monthlySpentByCategory = computed(() => {
  const now = new Date()
  const map: Record<string, number> = {}

  for (const tx of budgetTransactions.value) {
    if (tx.categoryType !== 'Expense') continue
    const d = new Date(tx.date)
    if (d.getFullYear() !== now.getFullYear() || d.getMonth() !== now.getMonth()) continue
    map[tx.categoryId] = (map[tx.categoryId] ?? 0) + Math.abs(tx.amount)
  }

  return map
})

const budgetHintByListId = computed(() => {
  const map: Record<string, { text: string; over: boolean }> = {}

  for (const list of shoppingLists.value) {
    const categoryId = selectedExpenseCategoryId.value[list.id]
    if (!categoryId) continue

    const category = categories.value.find((c) => c.id === categoryId)
    if (!category) continue

    const spent = monthlySpentByCategory.value[categoryId] ?? 0
    const estimate = list.items.reduce(
      (sum, item) => sum + (item.actualTotalPrice ?? item.estimatedTotalPrice ?? 0),
      0,
    )

    if (category.monthlyLimit == null) {
      map[list.id] = {
        text: `${category.name}: diesen Monat bereits ${formatMoney(spent)} ausgegeben · diese Liste: ~${formatMoney(estimate)}`,
        over: false,
      }
      continue
    }

    const remaining = category.monthlyLimit - spent
    map[list.id] = {
      text: `${category.name}: noch ${formatMoney(remaining)} von ${formatMoney(category.monthlyLimit)} übrig · diese Liste: ~${formatMoney(estimate)}`,
      over: estimate > remaining,
    }
  }

  return map
})

async function loadBudgetSpending() {
  try {
    budgetTransactions.value = await apiFetch<BudgetTransaction[]>('/transactions')
  } catch {
    // Offline oder Backend nicht erreichbar — der Budget-Hinweis wird dann einfach nicht angezeigt.
    budgetTransactions.value = []
  }
}

const selectedCatalogEstimate = computed(() => {
  if (!newItem.value.catalogItemId) {
    return null
  }

  return getCatalogEstimate(newItem.value.catalogItemId, Number(newItem.value.quantity))
})

const offlineSnapshotLabel = computed(() => {
  if (!offlineSnapshotAt.value) {
    return ''
  }

  return new Intl.DateTimeFormat('de-AT', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(offlineSnapshotAt.value))
})

const pendingMutationLabel = computed(() =>
  pendingMutationCount.value === 1
    ? '1 lokale Offline-Aenderung wartet auf Sync.'
    : `${pendingMutationCount.value} lokale Offline-Aenderungen warten auf Sync.`,
)
const syncStatusLabel = computed(() => {
  if (isSyncing.value) {
    return 'Synchronisiert'
  }

  if (!isOnline.value) {
    return pendingMutationCount.value > 0 ? 'Offline · wartet' : 'Offline'
  }

  if (pendingMutationCount.value > 0) {
    return 'Sync ausstehend'
  }

  return 'Online'
})
const syncStatusVariant = computed(() => {
  if (isSyncing.value) {
    return 'info'
  }

  if (!isOnline.value) {
    return pendingMutationCount.value > 0 ? 'warning' : 'secondary'
  }

  if (pendingMutationCount.value > 0) {
    return 'warning'
  }

  return 'success'
})

function isOfflineError(err: unknown) {
  if (typeof navigator !== 'undefined' && !navigator.onLine) {
    return true
  }

  if (err instanceof TypeError) {
    return true
  }

  return err instanceof Error && /fetch|network|offline|load/i.test(err.message)
}

function isTempId(id: string) {
  return id.startsWith('offline-')
}

function readPendingMutations() {
  return readOfflineMutations<ShoppingMutation>('shopping-lists')
}

function writePendingMutations(entries: ReturnType<typeof readPendingMutations>) {
  writeOfflineMutations('shopping-lists', entries)
  pendingMutationCount.value = entries.length
}

function refreshPendingMutationCount() {
  pendingMutationCount.value = readPendingMutations().length
}

function persistShoppingSnapshot() {
  saveOfflineCache('shopping-lists:view-data', {
    catalogItems: catalogItems.value,
    shoppingLists: shoppingLists.value,
    categories: categories.value,
  })
}

function sortShoppingListItems(list: ShoppingList) {
  list.items.sort((left, right) => left.name.localeCompare(right.name, 'de'))
}

function refreshLocalShoppingState() {
  if (selectedListId.value) {
    loadStoreSummaries()
  } else {
    storeSummaries.value = []
  }

  persistShoppingSnapshot()
}

function getCatalogItemName(catalogItemId?: string | null) {
  if (!catalogItemId) {
    return null
  }

  return catalogItems.value.find((item) => item.id === catalogItemId)?.name ?? null
}

function applyLocalItemUpsert(shoppingListId: string, item: ShoppingItem) {
  const list = shoppingLists.value.find((entry) => entry.id === shoppingListId)
  if (!list) {
    return
  }

  const existingIndex = list.items.findIndex((entry) => entry.id === item.id)
  if (existingIndex >= 0) {
    list.items.splice(existingIndex, 1, item)
  } else {
    list.items.push(item)
  }

  sortShoppingListItems(list)
  refreshLocalShoppingState()
}

function applyLocalItemRemoval(shoppingListId: string, itemId: string) {
  const list = shoppingLists.value.find((entry) => entry.id === shoppingListId)
  if (!list) {
    return
  }

  list.items = list.items.filter((entry) => entry.id !== itemId)
  refreshLocalShoppingState()
}

function applyLocalListUpsert(list: ShoppingList) {
  const existingIndex = shoppingLists.value.findIndex((entry) => entry.id === list.id)
  if (existingIndex >= 0) {
    shoppingLists.value.splice(existingIndex, 1, list)
  } else {
    shoppingLists.value.unshift(list)
  }

  shoppingLists.value.sort((left, right) => right.createdAt.localeCompare(left.createdAt))
  refreshLocalShoppingState()
}

function applyLocalListRemoval(listId: string) {
  shoppingLists.value = shoppingLists.value.filter((entry) => entry.id !== listId)
  if (selectedListId.value === listId) {
    selectedListId.value = shoppingLists.value[0]?.id ?? ''
  }

  delete completeAsBudgetExpense.value[listId]
  delete selectedExpenseCategoryId.value[listId]
  refreshLocalShoppingState()
}

function applyLocalPriceOptionUpsert(shoppingListId: string, itemId: string, priceOption: ShoppingItemPriceOption) {
  const item = shoppingLists.value
    .find((list) => list.id === shoppingListId)
    ?.items.find((entry) => entry.id === itemId)

  if (!item) {
    return
  }

  const existingIndex = item.priceOptions.findIndex((entry) => entry.id === priceOption.id)
  if (existingIndex >= 0) {
    item.priceOptions.splice(existingIndex, 1, priceOption)
  } else {
    item.priceOptions.push(priceOption)
  }

  item.priceOptions.sort((left, right) => left.totalPrice - right.totalPrice)
  refreshLocalShoppingState()
}

function applyLocalPriceOptionRemoval(shoppingListId: string, itemId: string, priceOptionId: string) {
  const item = shoppingLists.value
    .find((list) => list.id === shoppingListId)
    ?.items.find((entry) => entry.id === itemId)

  if (!item) {
    return
  }

  item.priceOptions = item.priceOptions.filter((entry) => entry.id !== priceOptionId)
  refreshLocalShoppingState()
}

function buildCreateItemPayload(): ShoppingItemPayload {
  const catalogEstimate = newItem.value.catalogItemId
    ? getCatalogEstimate(newItem.value.catalogItemId, Number(newItem.value.quantity))
    : null

  return {
    name: newItem.value.name.trim(),
    requiredQuantity: Number(newItem.value.requiredQuantity),
    inventoryQuantityUsed: Number(newItem.value.inventoryQuantityUsed),
    quantity: Number(newItem.value.quantity),
    purchasedQuantity: Number(newItem.value.purchasedQuantity),
    unit: newItem.value.unit.trim() || 'Stk',
    isChecked: false,
    estimatedUnitPrice: catalogEstimate?.estimatedUnitPrice ?? null,
    estimatedTotalPrice: catalogEstimate?.estimatedTotalPrice ?? null,
    actualTotalPrice: null,
    sourceType: newItem.value.sourceType,
    catalogItemId: newItem.value.catalogItemId || null,
  }
}

function buildUpdateItemPayload(item: ShoppingItem, overrides?: Partial<ShoppingItemPayload>): ShoppingItemPayload {
  return {
    name: overrides?.name ?? item.name,
    requiredQuantity: overrides?.requiredQuantity ?? item.requiredQuantity,
    inventoryQuantityUsed: overrides?.inventoryQuantityUsed ?? item.inventoryQuantityUsed,
    quantity: overrides?.quantity ?? item.quantity,
    purchasedQuantity: overrides?.purchasedQuantity ?? item.purchasedQuantity,
    unit: overrides?.unit ?? item.unit,
    isChecked: overrides?.isChecked ?? item.isChecked,
    estimatedUnitPrice: overrides?.estimatedUnitPrice ?? item.estimatedUnitPrice ?? null,
    estimatedTotalPrice: overrides?.estimatedTotalPrice ?? item.estimatedTotalPrice ?? null,
    actualTotalPrice: overrides?.actualTotalPrice ?? item.actualTotalPrice ?? null,
    sourceType: overrides?.sourceType ?? item.sourceType,
    catalogItemId: overrides?.catalogItemId ?? item.catalogItemId ?? null,
  }
}

function buildPriceOptionPayload(state: typeof editPriceOption.value | typeof newPriceOptionByItemId.value[string]): PriceOptionPayload {
  return {
    storeName: state.storeName.trim(),
    productName: state.productName.trim(),
    unitPrice: Number(state.unitPrice),
    totalPrice: Number(state.totalPrice),
    productUrl: state.productUrl?.trim() || null,
    isAvailable: state.isAvailable,
  }
}

function removeQueuedCreateList(listId: string) {
  const entries = readPendingMutations().filter(
    (entry) => entry.mutation.shoppingListId !== listId,
  )
  writePendingMutations(entries)
}

function removeQueuedCreateItem(itemId: string) {
  const entries = readPendingMutations().filter((entry) => {
    if (entry.mutation.type === 'createItem' && entry.mutation.itemId === itemId) {
      return false
    }

    if ('itemId' in entry.mutation && entry.mutation.itemId === itemId) {
      return false
    }

    return true
  })
  writePendingMutations(entries)
}

function removeQueuedCreatePriceOption(priceOptionId: string) {
  const entries = readPendingMutations().filter((entry) => {
    if (
      entry.mutation.type === 'createPriceOption' &&
      entry.mutation.priceOptionId === priceOptionId
    ) {
      return false
    }

    if ('priceOptionId' in entry.mutation && entry.mutation.priceOptionId === priceOptionId) {
      return false
    }

    return true
  })
  writePendingMutations(entries)
}

function resetNewItemForm() {
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
}

function updateQueuedCreateList(listId: string, payload: ShoppingListPayload) {
  const entries = readPendingMutations()
  const entry = entries.find(
    (candidate) => candidate.mutation.type === 'createList' && candidate.mutation.shoppingListId === listId,
  )

  if (!entry || entry.mutation.type !== 'createList') {
    return false
  }

  entry.mutation.payload = payload
  writePendingMutations(entries)
  return true
}

function updateQueuedCreateItem(itemId: string, payload: ShoppingItemPayload) {
  const entries = readPendingMutations()
  const entry = entries.find(
    (candidate) => candidate.mutation.type === 'createItem' && candidate.mutation.itemId === itemId,
  )

  if (!entry || entry.mutation.type !== 'createItem') {
    return false
  }

  entry.mutation.payload = payload
  writePendingMutations(entries)
  return true
}

function updateQueuedCreatePriceOption(priceOptionId: string, payload: PriceOptionPayload) {
  const entries = readPendingMutations()
  const entry = entries.find(
    (candidate) =>
      candidate.mutation.type === 'createPriceOption' &&
      candidate.mutation.priceOptionId === priceOptionId,
  )

  if (!entry || entry.mutation.type !== 'createPriceOption') {
    return false
  }

  entry.mutation.payload = payload
  writePendingMutations(entries)
  return true
}

function queueListUpdateMutation(shoppingListId: string, payload: ShoppingListPayload) {
  const nextEntries = readPendingMutations().filter(
    (entry) =>
      !(entry.mutation.shoppingListId === shoppingListId && entry.mutation.type === 'updateList'),
  )

  nextEntries.push({
    id: makeTempId('mutation-shopping'),
    createdAt: new Date().toISOString(),
    mutation: {
      type: 'updateList',
      shoppingListId,
      payload,
    },
  })

  writePendingMutations(nextEntries)
}

function queueListDeleteMutation(shoppingListId: string) {
  const nextEntries = readPendingMutations().filter(
    (entry) =>
      !(
        entry.mutation.shoppingListId === shoppingListId &&
        (entry.mutation.type === 'updateList' || entry.mutation.type === 'deleteList' || entry.mutation.type === 'completeList')
      ),
  )

  nextEntries.push({
    id: makeTempId('mutation-shopping'),
    createdAt: new Date().toISOString(),
    mutation: {
      type: 'deleteList',
      shoppingListId,
    },
  })

  writePendingMutations(nextEntries)
}

function queueListCompleteMutation(shoppingListId: string, payload: CompleteListPayload) {
  const nextEntries = readPendingMutations().filter(
    (entry) =>
      !(entry.mutation.shoppingListId === shoppingListId && entry.mutation.type === 'completeList'),
  )

  nextEntries.push({
    id: makeTempId('mutation-shopping'),
    createdAt: new Date().toISOString(),
    mutation: {
      type: 'completeList',
      shoppingListId,
      payload,
    },
  })

  writePendingMutations(nextEntries)
}

function queueUpdateMutation(shoppingListId: string, itemId: string, payload: ShoppingItemPayload) {
  const nextEntries = readPendingMutations().filter(
    (entry) =>
      !(
        entry.mutation.shoppingListId === shoppingListId &&
        entry.mutation.type === 'updateItem' &&
        entry.mutation.itemId === itemId
      ),
  )

  nextEntries.push({
    id: makeTempId('mutation-shopping'),
    createdAt: new Date().toISOString(),
    mutation: {
      type: 'updateItem',
      shoppingListId,
      itemId,
      payload,
    },
  })

  writePendingMutations(nextEntries)
}

function queueDeleteMutation(shoppingListId: string, itemId: string) {
  const nextEntries = readPendingMutations().filter(
    (entry) =>
      !(
        entry.mutation.shoppingListId === shoppingListId &&
        (entry.mutation.type === 'updateItem' || entry.mutation.type === 'deleteItem') &&
        entry.mutation.itemId === itemId
      ),
  )

  nextEntries.push({
    id: makeTempId('mutation-shopping'),
    createdAt: new Date().toISOString(),
    mutation: {
      type: 'deleteItem',
      shoppingListId,
      itemId,
    },
  })

  writePendingMutations(nextEntries)
}

function queuePriceOptionUpdateMutation(
  shoppingListId: string,
  itemId: string,
  priceOptionId: string,
  payload: PriceOptionPayload,
) {
  const nextEntries = readPendingMutations().filter(
    (entry) =>
      !(
        entry.mutation.shoppingListId === shoppingListId &&
        entry.mutation.type === 'updatePriceOption' &&
        entry.mutation.itemId === itemId &&
        entry.mutation.priceOptionId === priceOptionId
      ),
  )

  nextEntries.push({
    id: makeTempId('mutation-shopping'),
    createdAt: new Date().toISOString(),
    mutation: {
      type: 'updatePriceOption',
      shoppingListId,
      itemId,
      priceOptionId,
      payload,
    },
  })

  writePendingMutations(nextEntries)
}

function queuePriceOptionDeleteMutation(shoppingListId: string, itemId: string, priceOptionId: string) {
  const nextEntries = readPendingMutations().filter(
    (entry) =>
      !(
        entry.mutation.shoppingListId === shoppingListId &&
        (entry.mutation.type === 'updatePriceOption' || entry.mutation.type === 'deletePriceOption') &&
        entry.mutation.itemId === itemId &&
        entry.mutation.priceOptionId === priceOptionId
      ),
  )

  nextEntries.push({
    id: makeTempId('mutation-shopping'),
    createdAt: new Date().toISOString(),
    mutation: {
      type: 'deletePriceOption',
      shoppingListId,
      itemId,
      priceOptionId,
    },
  })

  writePendingMutations(nextEntries)
}

async function syncPendingShoppingMutations() {
  const entries = readPendingMutations()

  if (entries.length === 0) {
    return
  }

  isSyncing.value = true
  const remaining = [...entries]
  const listIdMap = new Map<string, string>()
  const itemIdMap = new Map<string, string>()
  const priceOptionIdMap = new Map<string, string>()
  let syncedAny = false

  while (remaining.length > 0) {
    const current = remaining[0]
    if (!current) {
      break
    }

    try {
      const resolvedListId = listIdMap.get(current.mutation.shoppingListId) ?? current.mutation.shoppingListId

      switch (current.mutation.type) {
        case 'createList': {
          const createdList = await apiFetch<ShoppingList>('/shoppinglists', {
            method: 'POST',
            body: JSON.stringify(current.mutation.payload),
          })
          listIdMap.set(current.mutation.shoppingListId, createdList.id)
          break
        }
        case 'updateList':
          await apiFetch<ShoppingList>(`/shoppinglists/${resolvedListId}`, {
            method: 'PUT',
            body: JSON.stringify(current.mutation.payload),
          })
          break
        case 'deleteList':
          await apiFetch<void>(`/shoppinglists/${resolvedListId}`, {
            method: 'DELETE',
          })
          break
        case 'completeList':
          await apiFetch<void>(`/shoppinglists/${resolvedListId}/complete`, {
            method: 'POST',
            body: JSON.stringify({
              ...current.mutation.payload,
              expenseCategoryId: current.mutation.payload.expenseCategoryId || null,
            }),
          })
          break
        case 'createItem':
        {
          const createdItem = await apiFetch<ShoppingItem>(`/shoppinglists/${resolvedListId}/items`, {
            method: 'POST',
            body: JSON.stringify(current.mutation.payload),
          })
          itemIdMap.set(current.mutation.itemId, createdItem.id)
          break
        }
        case 'updateItem':
        {
          const resolvedItemId = itemIdMap.get(current.mutation.itemId) ?? current.mutation.itemId
          await apiFetch<ShoppingItem>(
            `/shoppinglists/${resolvedListId}/items/${resolvedItemId}`,
            {
              method: 'PUT',
              body: JSON.stringify(current.mutation.payload),
            },
          )
          break
        }
        case 'deleteItem':
        {
          const resolvedItemId = itemIdMap.get(current.mutation.itemId) ?? current.mutation.itemId
          await apiFetch<void>(
            `/shoppinglists/${resolvedListId}/items/${resolvedItemId}`,
            {
              method: 'DELETE',
            },
          )
          break
        }
        case 'createPriceOption':
        {
          const resolvedItemId = itemIdMap.get(current.mutation.itemId) ?? current.mutation.itemId
          const createdOption = await apiFetch<ShoppingItemPriceOption>(
            `/shoppinglists/${resolvedListId}/items/${resolvedItemId}/price-options`,
            {
              method: 'POST',
              body: JSON.stringify(current.mutation.payload),
            },
          )
          priceOptionIdMap.set(current.mutation.priceOptionId, createdOption.id)
          break
        }
        case 'updatePriceOption':
        {
          const resolvedItemId = itemIdMap.get(current.mutation.itemId) ?? current.mutation.itemId
          const resolvedPriceOptionId =
            priceOptionIdMap.get(current.mutation.priceOptionId) ?? current.mutation.priceOptionId
          await apiFetch<ShoppingItemPriceOption>(
            `/shoppinglists/${resolvedListId}/items/${resolvedItemId}/price-options/${resolvedPriceOptionId}`,
            {
              method: 'PUT',
              body: JSON.stringify(current.mutation.payload),
            },
          )
          break
        }
        case 'deletePriceOption':
        {
          const resolvedItemId = itemIdMap.get(current.mutation.itemId) ?? current.mutation.itemId
          const resolvedPriceOptionId =
            priceOptionIdMap.get(current.mutation.priceOptionId) ?? current.mutation.priceOptionId
          await apiFetch<void>(
            `/shoppinglists/${resolvedListId}/items/${resolvedItemId}/price-options/${resolvedPriceOptionId}`,
            {
              method: 'DELETE',
            },
          )
          break
        }
      }

      remaining.shift()
      syncedAny = true
    } catch (err) {
      if (
        current.mutation.type !== 'createItem' &&
        current.mutation.type !== 'createList' &&
        current.mutation.type !== 'createPriceOption' &&
        err instanceof Error &&
        err.message.toLowerCase().includes('nicht gefunden')
      ) {
        remaining.shift()
        syncedAny = true
        continue
      }

      if (isOfflineError(err)) {
        break
      }

      error.value = err instanceof Error ? err.message : 'Offline-Sync fuer Einkaufsliste fehlgeschlagen.'
      break
    }
  }

  writePendingMutations(remaining)
  isSyncing.value = false

  if (syncedAny) {
    success.value = 'Offline-Aenderungen der Einkaufsliste wurden synchronisiert.'
    await loadData()
  }
}

async function handleOnlineShoppingSync() {
  isOnline.value = true
  if (typeof navigator !== 'undefined' && !navigator.onLine) {
    return
  }

  await syncPendingShoppingMutations()
}

function handleOfflineShoppingMode() {
  isOnline.value = false
}

function getCatalogSuggestions(query: string) {
  const normalizedQuery = query.trim().toLowerCase()

  return catalogItems.value
    .filter((item) => !normalizedQuery || item.name.toLowerCase().includes(normalizedQuery))
    .sort((left, right) => {
      const leftStartsWith = left.name.toLowerCase().startsWith(normalizedQuery)
      const rightStartsWith = right.name.toLowerCase().startsWith(normalizedQuery)

      if (leftStartsWith !== rightStartsWith) {
        return leftStartsWith ? -1 : 1
      }

      return left.name.localeCompare(right.name, 'de')
    })
    .slice(0, 8)
}

const catalogNameSuggestions = computed(() => getCatalogSuggestions(newItem.value.name))

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
    case 'MealPlanUpcoming':
      return 'Mealplanung'
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

function applyCatalogNameSuggestion() {
  const matchingCatalogItem = catalogItems.value.find(
    (item) => item.name.toLowerCase() === newItem.value.name.trim().toLowerCase(),
  )

  if (!matchingCatalogItem) {
    newItem.value.catalogItemId = ''
    return
  }

  newItem.value.name = matchingCatalogItem.name
  newItem.value.unit = matchingCatalogItem.defaultUnit
  newItem.value.catalogItemId = matchingCatalogItem.id
}

function getCatalogEstimate(catalogItemId: string, quantity: number) {
  const selectedCatalogItem = catalogItems.value.find((x) => x.id === catalogItemId)
  if (!selectedCatalogItem) {
    return null
  }

  const availablePrices = (selectedCatalogItem.prices ?? [])
    .filter((price) => price.isAvailable)
    .sort((left, right) => {
      const leftTotal = left.totalPrice ?? Number.POSITIVE_INFINITY
      const rightTotal = right.totalPrice ?? Number.POSITIVE_INFINITY

      if (leftTotal !== rightTotal) {
        return leftTotal - rightTotal
      }

      return (left.unitPrice ?? Number.POSITIVE_INFINITY) - (right.unitPrice ?? Number.POSITIVE_INFINITY)
    })

  const bestPrice = availablePrices[0] ?? null
  if (!bestPrice) {
    return null
  }

  const normalizedQuantity = Number.isFinite(quantity) && quantity > 0 ? quantity : 1
  const estimatedUnitPrice = bestPrice.unitPrice ?? null
  const estimatedTotalPrice =
    bestPrice.totalPrice != null
      ? bestPrice.totalPrice * normalizedQuantity
      : estimatedUnitPrice != null
        ? estimatedUnitPrice * normalizedQuantity
        : null

  return {
    storeName: bestPrice.storeName,
    productName: bestPrice.productName,
    estimatedUnitPrice,
    estimatedTotalPrice,
    productUrl: bestPrice.productUrl ?? null,
  }
}

function normalizeMatchText(value: string) {
  return value
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .trim()
}

function scoreCatalogCandidate(query: string, candidate: CatalogItem) {
  const haystacks = [candidate.name, candidate.searchTerm ?? '', candidate.brandHint ?? '']
  let score = 0

  for (const haystack of haystacks) {
    const normalized = normalizeMatchText(haystack)
    if (!normalized) {
      continue
    }

    if (normalized === query) {
      score = Math.max(score, 100)
    } else if (normalized.startsWith(query) || query.startsWith(normalized)) {
      score = Math.max(score, 80)
    } else if (normalized.includes(query) || query.includes(normalized)) {
      score = Math.max(score, 60)
    }
  }

  return score
}

function matchCatalogItem(rawName: string): { item: CatalogItem; score: number } | null {
  const query = normalizeMatchText(rawName)
  if (!query) {
    return null
  }

  let best: { item: CatalogItem; score: number } | null = null

  for (const candidate of catalogItems.value) {
    const score = scoreCatalogCandidate(query, candidate)
    if (score > 0 && (!best || score > best.score)) {
      best = { item: candidate, score }
    }
  }

  return best
}

// Erkennt führende Menge/Einheit wie „2 Milch“ oder „3 kg Kartoffeln“; sonst Menge 1.
function parseNotebookLine(raw: string): { name: string; quantity: number; unit: string | null } {
  const trimmed = raw.trim()
  const match = trimmed.match(
    /^(\d+(?:[.,]\d+)?)\s*(stk|stück|x|kg|g|l|ml|dose|dosen|pkg|packung|packungen)?\s+(.+)$/i,
  )

  const [, amount, unit, rest] = match ?? []
  if (amount && rest) {
    return {
      quantity: Number(amount.replace(',', '.')),
      unit: unit ? unit.trim() : null,
      name: rest.trim(),
    }
  }

  return { name: trimmed, quantity: 1, unit: null }
}

async function createCatalogItemFromName(name: string, unit: string): Promise<CatalogItem | null> {
  try {
    const created = await apiFetch<CatalogItem>('/catalog', {
      method: 'POST',
      body: JSON.stringify({
        name,
        defaultUnit: unit || 'Stk',
        category: null,
        searchTerm: name,
        brandHint: null,
        isStaple: false,
      }),
    })

    catalogItems.value.push(created)
    return created
  } catch (err) {
    // Offline oder Fehler: Artikel wird ohne Katalog-Verknüpfung angelegt.
    console.error(err)
    return null
  }
}

async function addNotebookItem(text: string) {
  const parsed = parseNotebookLine(text)
  if (!parsed.name) {
    return
  }

  const match = matchCatalogItem(parsed.name)
  let catalogItemId = ''
  let unit = parsed.unit ?? ''

  if (match && match.score >= MATCH_THRESHOLD) {
    catalogItemId = match.item.id
    if (!unit) {
      unit = match.item.defaultUnit
    }
  } else {
    // Eindeutig neuer Eintrag → neuen Katalogeintrag anlegen.
    const created = await createCatalogItemFromName(parsed.name, unit || 'Stk')
    if (created) {
      catalogItemId = created.id
      if (!unit) {
        unit = created.defaultUnit
      }
    }
  }

  newItem.value = {
    name: parsed.name,
    requiredQuantity: parsed.quantity,
    inventoryQuantityUsed: 0,
    quantity: parsed.quantity,
    purchasedQuantity: 0,
    unit: unit || 'Stk',
    sourceType: 'Manual',
    catalogItemId,
  }

  await createItem()
}

async function commitNewLine() {
  const text = newLineText.value.trim()
  if (!text || !selectedListId.value) {
    return
  }

  newLineText.value = ''
  await addNotebookItem(text)
}

async function commitItemName(item: ShoppingItem, rawName: string) {
  const text = rawName.trim()
  if (!text || text === item.name || !selectedListId.value) {
    return
  }

  const match = matchCatalogItem(text)
  const catalogItemId =
    match && match.score >= MATCH_THRESHOLD ? match.item.id : item.catalogItemId ?? null
  const payload = buildUpdateItemPayload(item, { name: text, catalogItemId })

  if (isTempId(item.id)) {
    updateQueuedCreateItem(item.id, payload)
    applyLocalItemUpsert(item.shoppingListId, {
      ...item,
      ...payload,
      catalogItemName: getCatalogItemName(payload.catalogItemId),
    })
    return
  }

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items/${item.id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
    await loadData()
  } catch (err) {
    console.error(err)
    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Artikel konnte nicht aktualisiert werden.'
      return
    }
    queueUpdateMutation(selectedListId.value, item.id, payload)
    applyLocalItemUpsert(item.shoppingListId, {
      ...item,
      ...payload,
      catalogItemName: getCatalogItemName(payload.catalogItemId),
    })
    success.value = 'Aenderung offline vorgemerkt.'
  }
}

function onNotebookBackspace(item: ShoppingItem, event: KeyboardEvent) {
  const input = event.target as HTMLInputElement
  if (input.value === '') {
    event.preventDefault()
    void deleteItem(item.id)
  }
}

function toggleMatchingMode() {
  matchingMode.value = matchingMode.value === 'client' ? 'llm' : 'client'
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

function loadStoreSummaries() {
  const items = selectedList.value?.items ?? []

  const storeMap = new Map<string, ShoppingListStoreSummary>()

  for (const item of items) {
    const quantity = item.quantity > 0 ? item.quantity : 1
    const cheapestByStore = new Map<string, number>()

    const considerPrice = (storeName: string, total: number) => {
      const existing = cheapestByStore.get(storeName)
      if (existing == null || total < existing) {
        cheapestByStore.set(storeName, total)
      }
    }

    // Manuell gepflegte Preisoptionen des Artikels
    for (const option of item.priceOptions) {
      if (option.isAvailable && option.totalPrice != null) {
        considerPrice(option.storeName, option.totalPrice)
      }
    }

    // Katalogpreise des automatisch zugeordneten Artikels (× Menge)
    const catalogItem = item.catalogItemId
      ? catalogItems.value.find((entry) => entry.id === item.catalogItemId)
      : undefined

    for (const price of catalogItem?.prices ?? []) {
      if (!price.isAvailable) {
        continue
      }
      const unitTotal = price.totalPrice ?? price.unitPrice
      if (unitTotal != null) {
        considerPrice(price.storeName, unitTotal * quantity)
      }
    }

    for (const [storeName, total] of cheapestByStore) {
      const current = storeMap.get(storeName) ?? {
        storeName,
        totalEstimatedPrice: 0,
        coveredItemsCount: 0,
        totalItemsCount: items.length,
        isComplete: false,
        isBestOption: false,
      }

      current.totalEstimatedPrice += total
      current.coveredItemsCount += 1
      storeMap.set(storeName, current)
    }
  }

  const summaries = Array.from(storeMap.values())
    .map((summary) => ({
      ...summary,
      isComplete: summary.coveredItemsCount === summary.totalItemsCount && summary.totalItemsCount > 0,
    }))
    .sort((left, right) => {
      if (left.isComplete !== right.isComplete) {
        return left.isComplete ? -1 : 1
      }

      return left.totalEstimatedPrice - right.totalEstimatedPrice
    })

  const bestCompleteSummary = summaries.find((summary) => summary.isComplete)

  if (bestCompleteSummary) {
    bestCompleteSummary.isBestOption = true
  }

  storeSummaries.value = summaries
}

async function loadData() {
  loading.value = true
  error.value = ''
  success.value = ''
  offlineSnapshotAt.value = null

  try {
    const [loadedCatalogItems, loadedShoppingLists, loadedCategories] = await Promise.all([
      apiFetch<CatalogItem[]>('/catalog'),
      apiFetch<ShoppingList[]>('/shoppinglists'),
      apiFetch<Category[]>('/categories'),
    ])

    catalogItems.value = loadedCatalogItems
    shoppingLists.value = loadedShoppingLists
    categories.value = loadedCategories
    saveOfflineCache('shopping-lists:view-data', {
      catalogItems: loadedCatalogItems,
      shoppingLists: loadedShoppingLists,
      categories: loadedCategories,
    })

    for (const list of loadedShoppingLists) {
      if (!selectedExpenseCategoryId.value[list.id]) {
        selectedExpenseCategoryId.value[list.id] = pickDefaultExpenseCategoryId(loadedCategories)
      }
    }

    if (!selectedListId.value && loadedShoppingLists.length > 0) {
      selectedListId.value = loadedShoppingLists[0]?.id ?? ''
    }

    if (selectedListId.value && !loadedShoppingLists.some((x) => x.id === selectedListId.value)) {
      selectedListId.value = loadedShoppingLists[0]?.id ?? ''
    }

    if (selectedListId.value) {
      loadStoreSummaries()
    } else {
      storeSummaries.value = []
    }
  } catch (err) {
    console.error(err)

    const cached = readOfflineCache<{
      catalogItems: CatalogItem[]
      shoppingLists: ShoppingList[]
      categories: Category[]
    }>('shopping-lists:view-data')

    if (!cached) {
      error.value = err instanceof Error ? err.message : 'Einkaufslisten konnten nicht geladen werden.'
      return
    }

    catalogItems.value = cached.value.catalogItems
    shoppingLists.value = cached.value.shoppingLists
    categories.value = cached.value.categories
    offlineSnapshotAt.value = cached.updatedAt
    error.value = 'Offline-Modus: Es werden die zuletzt geladenen Einkaufsdaten angezeigt.'

    for (const list of cached.value.shoppingLists) {
      if (!selectedExpenseCategoryId.value[list.id]) {
        selectedExpenseCategoryId.value[list.id] = pickDefaultExpenseCategoryId(cached.value.categories)
      }
    }

    if (!selectedListId.value && cached.value.shoppingLists.length > 0) {
      selectedListId.value = cached.value.shoppingLists[0]?.id ?? ''
    }

    if (
      selectedListId.value &&
      !cached.value.shoppingLists.some((x) => x.id === selectedListId.value)
    ) {
      selectedListId.value = cached.value.shoppingLists[0]?.id ?? ''
    }

    if (selectedListId.value) {
      loadStoreSummaries()
    } else {
      storeSummaries.value = []
    }
  } finally {
    loading.value = false
  }
}

async function createList() {
  error.value = ''
  success.value = ''
  const payload: ShoppingListPayload = {
    name: newList.value.name.trim(),
  }

  try {
    const created = await apiFetch<ShoppingList>('/shoppinglists', {
      method: 'POST',
      body: JSON.stringify(payload),
    })

    newList.value.name = ''
    await loadData()
    selectedListId.value = created.id
    success.value = 'Einkaufsliste wurde erstellt.'
  } catch (err) {
    console.error(err)

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Einkaufsliste konnte nicht erstellt werden.'
      return
    }

    const tempListId = makeTempId('offline-list')
    enqueueOfflineMutation<ShoppingMutation>('shopping-lists', {
      type: 'createList',
      shoppingListId: tempListId,
      payload,
    })
    refreshPendingMutationCount()

    applyLocalListUpsert({
      id: tempListId,
      name: payload.name,
      createdAt: new Date().toISOString(),
      items: [],
    })
    selectedListId.value = tempListId
    newList.value.name = ''
    success.value = 'Einkaufsliste offline angelegt und zum Sync vorgemerkt.'
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
  const payload: ShoppingListPayload = {
    name: editList.value.name.trim(),
  }

  if (isTempId(id)) {
    updateQueuedCreateList(id, payload)
    const list = shoppingLists.value.find((entry) => entry.id === id)
    if (list) {
      list.name = payload.name
      applyLocalListUpsert({ ...list })
    }
    cancelEditList()
    success.value = 'Lokale Einkaufsliste wurde aktualisiert.'
    return
  }

  try {
    await apiFetch<ShoppingList>(`/shoppinglists/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })

    cancelEditList()
    await loadData()
    success.value = 'Einkaufsliste wurde aktualisiert.'
  } catch (err) {
    console.error(err)

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Einkaufsliste konnte nicht aktualisiert werden.'
      return
    }

    queueListUpdateMutation(id, payload)
    const list = shoppingLists.value.find((entry) => entry.id === id)
    if (list) {
      list.name = payload.name
      applyLocalListUpsert({ ...list })
    }
    cancelEditList()
    success.value = 'Einkaufsliste offline aktualisiert und zum Sync vorgemerkt.'
  }
}

async function deleteList(id: string) {
  error.value = ''
  success.value = ''

  if (isTempId(id)) {
    removeQueuedCreateList(id)
    if (editListId.value === id) {
      cancelEditList()
    }
    applyLocalListRemoval(id)
    success.value = 'Lokale Einkaufsliste wurde entfernt.'
    return
  }

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

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Einkaufsliste konnte nicht gelöscht werden.'
      return
    }

    if (editListId.value === id) {
      cancelEditList()
    }
    queueListDeleteMutation(id)
    applyLocalListRemoval(id)
    success.value = 'Einkaufsliste offline entfernt und zum Sync vorgemerkt.'
  }
}

async function createItem() {
  if (!selectedListId.value) {
    error.value = 'Bitte zuerst eine Einkaufsliste auswählen.'
    return
  }

  error.value = ''
  success.value = ''
  const payload = buildCreateItemPayload()

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items`, {
      method: 'POST',
      body: JSON.stringify(payload),
    })

    resetNewItemForm()

    await loadData()
    success.value = 'Artikel wurde erstellt.'
  } catch (err) {
    console.error(err)

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Artikel konnte nicht erstellt werden.'
      return
    }

    const tempItemId = makeTempId('offline-item')
    enqueueOfflineMutation<ShoppingMutation>('shopping-lists', {
      type: 'createItem',
      shoppingListId: selectedListId.value,
      itemId: tempItemId,
      payload,
    })
    refreshPendingMutationCount()

    applyLocalItemUpsert(selectedListId.value, {
      id: tempItemId,
      shoppingListId: selectedListId.value,
      name: payload.name,
      requiredQuantity: payload.requiredQuantity,
      inventoryQuantityUsed: payload.inventoryQuantityUsed,
      quantity: payload.quantity,
      purchasedQuantity: payload.purchasedQuantity,
      unit: payload.unit,
      isChecked: payload.isChecked,
      estimatedUnitPrice: payload.estimatedUnitPrice,
      estimatedTotalPrice: payload.estimatedTotalPrice,
      actualTotalPrice: payload.actualTotalPrice,
      sourceType: payload.sourceType,
      catalogItemId: payload.catalogItemId ?? null,
      catalogItemName: getCatalogItemName(payload.catalogItemId),
      priceOptions: [],
    })

    resetNewItemForm()
    success.value = 'Artikel offline vorgemerkt und lokal gespeichert.'
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
  const payload = buildUpdateItemPayload(item, {
    purchasedQuantity: Number(editItem.value.purchasedQuantity),
    actualTotalPrice:
      editItem.value.actualTotalPrice == null ? null : Number(editItem.value.actualTotalPrice),
    isChecked: editItem.value.isChecked,
  })

  if (isTempId(item.id)) {
    updateQueuedCreateItem(item.id, payload)
    applyLocalItemUpsert(item.shoppingListId, {
      ...item,
      ...payload,
      catalogItemName: getCatalogItemName(payload.catalogItemId),
    })
    cancelEditItem()
    success.value = 'Lokaler Artikel wurde aktualisiert.'
    return
  }

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items/${item.id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })

    cancelEditItem()
    await loadData()
    success.value = 'Einkaufsdaten wurden aktualisiert.'
  } catch (err) {
    console.error(err)

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Artikel konnte nicht aktualisiert werden.'
      return
    }

    queueUpdateMutation(selectedListId.value, item.id, payload)
    applyLocalItemUpsert(item.shoppingListId, {
      ...item,
      ...payload,
      catalogItemName: getCatalogItemName(payload.catalogItemId),
    })
    cancelEditItem()
    success.value = 'Artikel offline aktualisiert und zum Sync vorgemerkt.'
  }
}

async function toggleItem(item: ShoppingItem) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''
  success.value = ''
  const payload = buildUpdateItemPayload(item, {
    isChecked: !item.isChecked,
  })

  if (isTempId(item.id)) {
    updateQueuedCreateItem(item.id, payload)
    applyLocalItemUpsert(item.shoppingListId, {
      ...item,
      ...payload,
      catalogItemName: getCatalogItemName(payload.catalogItemId),
    })
    return
  }

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items/${item.id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })

    await loadData()
  } catch (err) {
    console.error(err)

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Artikel konnte nicht aktualisiert werden.'
      return
    }

    queueUpdateMutation(selectedListId.value, item.id, payload)
    applyLocalItemUpsert(item.shoppingListId, {
      ...item,
      ...payload,
      catalogItemName: getCatalogItemName(payload.catalogItemId),
    })
    success.value = 'Aenderung offline vorgemerkt.'
  }
}

async function deleteItem(itemId: string) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''
  success.value = ''

  const localItem = selectedList.value?.items.find((item) => item.id === itemId) ?? null

  if (localItem && isTempId(itemId)) {
    const nextEntries = readPendingMutations().filter(
      (entry) => !(entry.mutation.type === 'createItem' && entry.mutation.itemId === itemId),
    )
    writePendingMutations(nextEntries)
    applyLocalItemRemoval(localItem.shoppingListId, itemId)
    if (editItemId.value === itemId) {
      cancelEditItem()
    }
    success.value = 'Lokaler Artikel wurde entfernt.'
    return
  }

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

    if (!isOfflineError(err) || !localItem) {
      error.value = err instanceof Error ? err.message : 'Artikel konnte nicht gelöscht werden.'
      return
    }

    queueDeleteMutation(selectedListId.value, itemId)

    if (editItemId.value === itemId) {
      cancelEditItem()
    }

    applyLocalItemRemoval(localItem.shoppingListId, itemId)
    success.value = 'Artikel offline entfernt und zum Sync vorgemerkt.'
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
  const payload: CompleteListPayload = {
    createBudgetExpense,
    expenseCategoryId: createBudgetExpense ? expenseCategoryId : null,
    transactionTitle: list ? `Einkauf · ${list.name}` : 'Einkauf',
    transactionDate: new Date().toISOString().slice(0, 10),
  }

  try {
    await apiFetch(`/shoppinglists/${id}/complete`, {
      method: 'POST',
      body: JSON.stringify(payload),
    })

    success.value = createBudgetExpense
      ? 'Einkauf wurde ins Inventar übernommen und zusätzlich als Budget-Ausgabe gespeichert.'
      : 'Einkauf wurde mit den tatsächlich gekauften Mengen ins Inventar übernommen.'

    await loadData()
  } catch (err) {
    console.error(err)

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Fehler beim Abschließen der Einkaufsliste.'
      return
    }

    queueListCompleteMutation(id, payload)
    success.value = 'Abschluss offline vorgemerkt und wird spaeter synchronisiert.'
  }
}

// ── Beleg-Scan (Foto → Claude → Preise/Inventar/Budget) ──

type ReceiptLine = {
  name: string
  quantity: number
  unitPrice: number | null
  totalPrice: number
  shoppingItemId: string | null
  catalogItemId: string | null
}

type ReceiptScanResult = {
  storeName: string | null
  purchaseDate: string | null
  totalAmount: number | null
  lines: ReceiptLine[]
}

const receiptFileInput = ref<HTMLInputElement | null>(null)
const receiptListId = ref('')
const receiptScanning = ref(false)
const receiptApplying = ref(false)
const showReceiptModal = ref(false)
const receiptResult = ref<ReceiptScanResult | null>(null)
const receiptCompleteList = ref(true)

const receiptShoppingItems = computed(() =>
  shoppingLists.value.find((x) => x.id === receiptListId.value)?.items ?? [],
)

const receiptLinesTotal = computed(() =>
  (receiptResult.value?.lines ?? []).reduce((sum, line) => sum + (line.totalPrice || 0), 0),
)

function startReceiptScan(listId: string) {
  if (!isOnline.value) {
    error.value = 'Der Beleg-Scan benötigt eine Internetverbindung.'
    return
  }

  receiptListId.value = listId
  receiptFileInput.value?.click()
}

function readFileAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(file)
  })
}

async function onReceiptFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''

  if (!file || !receiptListId.value) {
    return
  }

  error.value = ''
  success.value = ''
  receiptScanning.value = true

  try {
    const dataUrl = await readFileAsDataUrl(file)
    const imageBase64 = dataUrl.slice(dataUrl.indexOf(',') + 1)

    const result = await apiFetch<ReceiptScanResult>('/receipts/scan', {
      method: 'POST',
      body: JSON.stringify({
        imageBase64,
        mediaType: file.type || 'image/jpeg',
        shoppingListId: receiptListId.value,
      }),
    })

    receiptResult.value = result
    showReceiptModal.value = true

    if (result.lines.length === 0) {
      error.value = 'Auf dem Beleg wurden keine Artikel erkannt.'
    }
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Beleg-Scan fehlgeschlagen.'
  } finally {
    receiptScanning.value = false
  }
}

function removeReceiptLine(index: number) {
  receiptResult.value?.lines.splice(index, 1)
}

function closeReceiptModal() {
  showReceiptModal.value = false
  receiptResult.value = null
}

async function applyReceipt() {
  if (!receiptResult.value || !receiptListId.value) {
    return
  }

  const createBudgetExpense =
    receiptCompleteList.value && (completeAsBudgetExpense.value[receiptListId.value] ?? false)
  const expenseCategoryId = selectedExpenseCategoryId.value[receiptListId.value] ?? ''

  if (createBudgetExpense && !expenseCategoryId) {
    error.value = 'Bitte eine Budget-Kategorie auswählen.'
    return
  }

  error.value = ''
  success.value = ''
  receiptApplying.value = true

  try {
    await apiFetch<void>('/receipts/apply', {
      method: 'POST',
      body: JSON.stringify({
        shoppingListId: receiptListId.value,
        storeName: receiptResult.value.storeName,
        lines: receiptResult.value.lines,
        completeList: receiptCompleteList.value,
        complete: receiptCompleteList.value
          ? {
              createBudgetExpense,
              expenseCategoryId: createBudgetExpense ? expenseCategoryId : null,
              transactionTitle: `Einkauf · ${receiptResult.value.storeName ?? 'Beleg'}`,
              transactionDate: receiptResult.value.purchaseDate ?? new Date().toISOString().slice(0, 10),
            }
          : null,
      }),
    })

    closeReceiptModal()
    success.value = receiptCompleteList.value
      ? 'Beleg übernommen: Preise aktualisiert, Inventar und Budget gebucht.'
      : 'Beleg übernommen: Preise wurden aktualisiert.'
    await loadData()
    await loadBudgetSpending()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Beleg konnte nicht übernommen werden.'
  } finally {
    receiptApplying.value = false
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
  const payload = buildPriceOptionPayload(state)

  try {
    await apiFetch<ShoppingItemPriceOption>(
      `/shoppinglists/${selectedListId.value}/items/${item.id}/price-options`,
      {
        method: 'POST',
        body: JSON.stringify(payload),
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

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Preisoption konnte nicht erstellt werden.'
      return
    }

    const tempPriceOptionId = makeTempId('offline-price-option')
    enqueueOfflineMutation<ShoppingMutation>('shopping-lists', {
      type: 'createPriceOption',
      shoppingListId: selectedListId.value,
      itemId: item.id,
      priceOptionId: tempPriceOptionId,
      payload,
    })
    refreshPendingMutationCount()

    applyLocalPriceOptionUpsert(selectedListId.value, item.id, {
      id: tempPriceOptionId,
      shoppingItemId: item.id,
      checkedAt: new Date().toISOString(),
      ...payload,
    })

    newPriceOptionByItemId.value[item.id] = {
      storeName: '',
      productName: item.catalogItemName || item.name,
      unitPrice: 0,
      totalPrice: 0,
      productUrl: '',
      isAvailable: true,
    }

    success.value = 'Preisoption offline gespeichert und zum Sync vorgemerkt.'
  }
}

async function updatePriceOption(item: ShoppingItem, priceOptionId: string) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''
  success.value = ''
  const payload = buildPriceOptionPayload(editPriceOption.value)

  if (isTempId(priceOptionId)) {
    updateQueuedCreatePriceOption(priceOptionId, payload)
    applyLocalPriceOptionUpsert(selectedListId.value, item.id, {
      id: priceOptionId,
      shoppingItemId: item.id,
      checkedAt: new Date().toISOString(),
      ...payload,
    })
    cancelEditPriceOption()
    success.value = 'Lokale Preisoption wurde aktualisiert.'
    return
  }

  try {
    await apiFetch<ShoppingItemPriceOption>(
      `/shoppinglists/${selectedListId.value}/items/${item.id}/price-options/${priceOptionId}`,
      {
        method: 'PUT',
        body: JSON.stringify(payload),
      },
    )

    cancelEditPriceOption()
    success.value = 'Preisoption wurde aktualisiert.'
    await loadData()
  } catch (err) {
    console.error(err)

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Preisoption konnte nicht aktualisiert werden.'
      return
    }

    queuePriceOptionUpdateMutation(selectedListId.value, item.id, priceOptionId, payload)
    applyLocalPriceOptionUpsert(selectedListId.value, item.id, {
      id: priceOptionId,
      shoppingItemId: item.id,
      checkedAt: new Date().toISOString(),
      ...payload,
    })
    cancelEditPriceOption()
    success.value = 'Preisoption offline aktualisiert und zum Sync vorgemerkt.'
  }
}

async function deletePriceOption(item: ShoppingItem, priceOptionId: string) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''
  success.value = ''

  if (isTempId(priceOptionId)) {
    removeQueuedCreatePriceOption(priceOptionId)
    if (editPriceOptionId.value === priceOptionId) {
      cancelEditPriceOption()
    }
    applyLocalPriceOptionRemoval(selectedListId.value, item.id, priceOptionId)
    success.value = 'Lokale Preisoption wurde entfernt.'
    return
  }

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

    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Preisoption konnte nicht gelöscht werden.'
      return
    }

    queuePriceOptionDeleteMutation(selectedListId.value, item.id, priceOptionId)

    if (editPriceOptionId.value === priceOptionId) {
      cancelEditPriceOption()
    }

    applyLocalPriceOptionRemoval(selectedListId.value, item.id, priceOptionId)
    success.value = 'Preisoption offline entfernt und zum Sync vorgemerkt.'
  }
}

onMounted(async () => {
  refreshPendingMutationCount()
  window.addEventListener('online', handleOnlineShoppingSync)
  window.addEventListener('offline', handleOfflineShoppingMode)
  await loadData()
  await loadBudgetSpending()
  await syncPendingShoppingMutations()
})

onUnmounted(() => {
  window.removeEventListener('online', handleOnlineShoppingSync)
  window.removeEventListener('offline', handleOfflineShoppingMode)
})
</script>

<template>
  <BContainer fluid="lg" class="py-4 d-flex flex-column gap-4">
    <div class="d-flex justify-content-between align-items-end flex-wrap gap-3">
      <div>
        <h1 class="h2 fw-bold mb-1">Einkaufsliste <span class="text-primary">Marktzettel</span></h1>
        <p class="text-secondary mb-0">Charmant wie ein echter Einkaufszettel, aber mit Preisen, Quellen und Inventar-Abgleich.</p>
      </div>
      <BBadge :variant="syncStatusVariant">{{ syncStatusLabel }}</BBadge>
    </div>

    <BAlert :model-value="!!error" variant="danger">{{ error }}</BAlert>
    <BAlert :model-value="!!success" variant="success">{{ success }}</BAlert>
    <BAlert :model-value="pendingMutationCount > 0" variant="info">{{ pendingMutationLabel }}</BAlert>
    <BAlert :model-value="!!offlineSnapshotAt" variant="info">Offline-Snapshot von {{ offlineSnapshotLabel }}</BAlert>
    <BAlert :model-value="loading" variant="info">Lade Einkaufslisten…</BAlert>

    <BRow class="g-3">
      <BCol md="3" sm="6">
        <BCard><div class="text-secondary text-uppercase small">🧾 Listen</div><div class="fs-4 fw-bold">{{ shoppingLists.length }}</div></BCard>
      </BCol>
      <BCol md="3" sm="6">
        <BCard><div class="text-secondary text-uppercase small">🛍️ Ausgewählte Liste</div><div class="fs-4 fw-bold">{{ selectedList?.items.length ?? 0 }}</div></BCard>
      </BCol>
      <BCol md="3" sm="6">
        <BCard><div class="text-secondary text-uppercase small">💶 Geschätzt</div><div class="fs-4 fw-bold">{{ formatMoney(selectedListTotals.estimated) }}</div></BCard>
      </BCol>
      <BCol md="3" sm="6">
        <BCard><div class="text-secondary text-uppercase small">🧮 Tatsächlich</div><div class="fs-4 fw-bold">{{ formatMoney(selectedListTotals.actual) }}</div></BCard>
      </BCol>
    </BRow>

    <BRow class="g-4">
      <BCol lg="5">
        <div class="d-flex flex-column gap-4">
          <BCard title="Neue Einkaufsliste">
            <p class="text-secondary small">Lege eine neue Liste an und öffne sie direkt für Artikel und Preisvorschläge.</p>
            <BForm @submit.prevent="createList">
              <BFormInput v-model="newList.name" type="text" placeholder="Name der Liste" required class="mb-3" />
              <BButton type="submit" variant="primary">Liste anlegen</BButton>
            </BForm>
          </BCard>

          <BCard title="Listenübersicht">
            <p class="text-secondary small">Eine Liste öffnen, um Artikel und Preisoptionen zu sehen.</p>

            <div v-if="shoppingLists.length > 0" class="d-flex flex-column gap-3">
              <BCard
                v-for="list in shoppingLists"
                :key="list.id"
                class="bg-body-tertiary"
                :class="{ 'border-primary': selectedListId === list.id }"
              >
                <template v-if="editListId === list.id">
                  <BFormInput v-model="editList.name" type="text" class="mb-2" />
                  <div class="d-flex gap-2 flex-wrap">
                    <BButton size="sm" variant="primary" @click="updateList(list.id)">Speichern</BButton>
                    <BButton size="sm" variant="outline-secondary" @click="cancelEditList">Abbrechen</BButton>
                  </div>
                </template>
                <template v-else>
                  <div class="d-flex justify-content-between align-items-start gap-2" role="button" @click="selectedListId = list.id">
                    <div>
                      <div class="fw-bold">{{ list.name }}</div>
                      <div class="text-secondary small">{{ new Date(list.createdAt).toLocaleDateString('de-AT') }}</div>
                    </div>
                    <BBadge variant="primary">{{ list.items.length }} Artikel</BBadge>
                  </div>

                  <div class="d-flex gap-2 flex-wrap my-2">
                    <BBadge variant="light" text-color="dark" class="border">
                      Geschätzt <strong>{{ formatMoney(list.items.reduce((sum, item) => sum + (item.estimatedTotalPrice ?? 0), 0)) }}</strong>
                    </BBadge>
                    <BBadge variant="light" text-color="dark" class="border">
                      Tatsächlich <strong>{{ formatMoney(list.items.reduce((sum, item) => sum + (item.actualTotalPrice ?? 0), 0)) }}</strong>
                    </BBadge>
                  </div>

                  <div class="border rounded p-2 mb-2">
                    <BFormCheckbox v-model="completeAsBudgetExpense[list.id]">Als Budget-Ausgabe speichern</BFormCheckbox>
                    <BFormSelect
                      v-if="completeAsBudgetExpense[list.id]"
                      v-model="selectedExpenseCategoryId[list.id]"
                      class="mt-2"
                    >
                      <option value="">Budget-Kategorie wählen</option>
                      <option v-for="category in expenseCategories" :key="category.id" :value="category.id">{{ category.name }}</option>
                    </BFormSelect>
                    <p
                      v-if="completeAsBudgetExpense[list.id] && budgetHintByListId[list.id]"
                      class="small mt-2 mb-0"
                      :class="budgetHintByListId[list.id]!.over ? 'text-danger' : 'text-secondary'"
                    >
                      {{ budgetHintByListId[list.id]!.text }}
                    </p>
                  </div>

                  <div class="d-flex gap-2 flex-wrap">
                    <BButton size="sm" variant="primary" @click="selectedListId = list.id">Öffnen</BButton>
                    <BButton size="sm" variant="outline-secondary" :disabled="receiptScanning" @click="startReceiptScan(list.id)">
                      {{ receiptScanning && receiptListId === list.id ? '⏳ Scanne…' : '📷 Bon scannen' }}
                    </BButton>
                    <BButton size="sm" variant="outline-secondary" @click="startEditList(list)">Bearbeiten</BButton>
                    <BButton size="sm" variant="outline-secondary" @click="completeShoppingList(list.id)">Abschließen</BButton>
                    <BButton size="sm" variant="outline-danger" @click="deleteList(list.id)">Löschen</BButton>
                  </div>
                </template>
              </BCard>
            </div>

            <p v-else class="text-secondary mb-0">Noch keine Einkaufslisten vorhanden.</p>
          </BCard>

          <BCard v-if="selectedList" :title="`Artikel zu „${selectedList.name}“ hinzufügen`">
            <p class="text-secondary small">Die Liste bleibt charmant manuell, bekommt aber automatisch Inventar- und Preisdaten dazu.</p>

            <BForm @submit.prevent="createItem">
              <BFormSelect v-model="newItem.catalogItemId" class="mb-2" @change="applyCatalogSelection(newItem.catalogItemId, newItem)">
                <option value="">Katalogartikel optional wählen</option>
                <option v-for="catalogItem in catalogItems" :key="catalogItem.id" :value="catalogItem.id">{{ catalogItem.name }}</option>
              </BFormSelect>
              <input
                v-model="newItem.name"
                list="shopping-catalog-name-suggestions"
                type="text"
                class="form-control mb-2"
                placeholder="Name"
                required
                @input="applyCatalogNameSuggestion"
              />
              <datalist id="shopping-catalog-name-suggestions">
                <option
                  v-for="catalogItem in catalogNameSuggestions"
                  :key="catalogItem.id"
                  :value="catalogItem.name"
                  :label="`${catalogItem.name} · ${catalogItem.defaultUnit}`"
                />
              </datalist>
              <BRow class="g-2 mb-2">
                <BCol><BFormInput v-model="newItem.requiredQuantity" type="number" min="0" step="0.01" placeholder="Benötigt" required /></BCol>
                <BCol><BFormInput v-model="newItem.unit" type="text" placeholder="Einheit" required /></BCol>
              </BRow>
              <BAlert v-if="selectedCatalogEstimate" :model-value="true" variant="light" class="border py-2">
                Vorschlag aus Katalog: <strong>{{ selectedCatalogEstimate.storeName }}</strong>
                · {{ formatMoney(selectedCatalogEstimate.estimatedTotalPrice) }}
              </BAlert>
              <BButton type="submit" variant="primary">Artikel hinzufügen</BButton>
            </BForm>
          </BCard>

          <BCard v-if="selectedList" title="Notizblock">
            <p class="text-secondary small">Eine Zeile = ein Artikel. Tippen oder auf dem iPad mit Apple Pencil schreiben. Häkchen zum Abhaken; leere Zeile + Entfernen löscht den Artikel.</p>

            <p class="text-secondary small">
              Zuordnung: <strong>lokal im Browser</strong>
              <BButton variant="link" size="sm" class="p-0 align-baseline" @click="toggleMatchingMode">
                {{ matchingMode === 'client' ? 'auf KI umstellen' : 'auf lokal zurück' }}
              </BButton>
              <span v-if="matchingMode === 'llm'"> · KI-Zuordnung ist vorbereitet, aber noch nicht aktiv – es wird weiter lokal zugeordnet.</span>
            </p>

            <div class="d-flex flex-column gap-2">
              <div
                v-for="item in selectedList.items"
                :key="item.id"
                class="d-flex align-items-center gap-2 border-bottom pb-2"
                :class="{ checked: item.isChecked }"
              >
                <BFormCheckbox :model-value="item.isChecked" @change="toggleItem(item)" />
                <input
                  class="form-control form-control-sm flex-grow-1"
                  :value="item.name"
                  type="text"
                  enterkeyhint="next"
                  @keydown.enter.prevent="commitItemName(item, ($event.target as HTMLInputElement).value)"
                  @blur="commitItemName(item, ($event.target as HTMLInputElement).value)"
                  @keydown.delete="onNotebookBackspace(item, $event)"
                />
                <span class="text-secondary small text-nowrap">{{ item.quantity }} {{ item.unit }}</span>
                <BBadge v-if="item.catalogItemName" variant="secondary">{{ item.catalogItemName }}</BBadge>
                <BBadge v-else variant="warning">neu</BBadge>
              </div>

              <div class="d-flex align-items-center gap-2">
                <span class="text-secondary">+</span>
                <input
                  class="form-control form-control-sm"
                  v-model="newLineText"
                  type="text"
                  placeholder="Artikel schreiben …"
                  enterkeyhint="done"
                  @keydown.enter.prevent="commitNewLine"
                />
              </div>
            </div>
          </BCard>
        </div>
      </BCol>

      <BCol lg="7">
        <BCard v-if="selectedList" :title="`„${selectedList.name}“`">
          <p class="text-secondary small">Bedarf, Inventar-Abzug und Preisoptionen werden automatisch gerechnet. Den echten Einkauf pflegst du hier mit Charme nach.</p>

          <div class="d-flex gap-2 flex-wrap mb-3">
            <BBadge variant="light" text-color="dark" class="border">Geschätzt <strong>{{ formatMoney(selectedListTotals.estimated) }}</strong></BBadge>
            <BBadge variant="light" text-color="dark" class="border">Tatsächlich <strong>{{ formatMoney(selectedListTotals.actual) }}</strong></BBadge>
          </div>

          <BAlert v-if="bestCompleteStoreSummary" :model-value="true" variant="light" class="border py-2">
            Günstigster vollständiger Händler: <strong>{{ bestCompleteStoreSummary.storeName }}</strong>
            · {{ formatMoney(bestCompleteStoreSummary.totalEstimatedPrice) }}
          </BAlert>

          <div v-if="storeSummaries.length > 0" class="mb-3">
            <h3 class="h6">Händlervergleich</h3>
            <div class="d-flex flex-column gap-2">
              <div v-for="summary in storeSummaries" :key="summary.storeName" class="border rounded p-2 bg-body-tertiary">
                <div class="d-flex justify-content-between align-items-center mb-1">
                  <strong>{{ summary.storeName }}</strong>
                  <BBadge :variant="summary.isBestOption ? 'success' : summary.isComplete ? 'primary' : 'warning'">
                    {{ summary.isBestOption ? 'Beste Option' : summary.isComplete ? 'vollständig' : 'teilweise' }}
                  </BBadge>
                </div>
                <div class="text-secondary small">{{ summary.coveredItemsCount }} / {{ summary.totalItemsCount }} Artikel · {{ formatMoney(summary.totalEstimatedPrice) }}</div>
              </div>
            </div>
          </div>

          <p v-if="selectedList.items.length === 0" class="text-secondary">Noch keine Artikel auf dieser Einkaufsliste.</p>

          <div class="d-flex flex-column gap-3">
            <div v-for="item in selectedList.items" :key="item.id" class="border rounded p-3" :class="{ checked: item.isChecked }">
              <div class="d-flex justify-content-between align-items-start gap-2">
                <div>
                  <div class="fw-semibold">{{ item.name }}</div>
                  <div class="text-secondary small">{{ item.quantity }} {{ item.unit }}</div>
                </div>
                <BFormCheckbox :model-value="item.isChecked" @change="toggleItem(item)" />
              </div>

              <div class="d-flex gap-2 flex-wrap mt-2">
                <BBadge variant="light" text-color="dark" class="border">Geschätzt <strong>{{ formatMoney(item.estimatedTotalPrice) }}</strong></BBadge>
                <BBadge variant="light" text-color="dark" class="border">Echt <strong>{{ formatMoney(item.actualTotalPrice) }}</strong></BBadge>
              </div>

              <div class="text-secondary small mt-2">
                <span>{{ displaySource(item.sourceType) }}</span>
                <span v-if="item.catalogItemName">· {{ item.catalogItemName }}</span>
                <span>· Bedarf {{ item.requiredQuantity }} {{ item.unit }}</span>
                <span>· Inventar {{ item.inventoryQuantityUsed }}</span>
              </div>

              <div class="d-flex gap-2 flex-wrap mt-2">
                <BButton size="sm" variant="outline-secondary" @click="startEditItem(item)">Echten Einkauf pflegen</BButton>
                <BButton size="sm" variant="outline-secondary" @click="togglePriceOptions(item.id)">
                  {{ expandedPriceItemIds.has(item.id) ? '▾' : '▸' }} Preisoptionen ({{ item.priceOptions.length }})
                </BButton>
                <BButton size="sm" variant="outline-danger" @click="deleteItem(item.id)">Löschen</BButton>
              </div>

              <div v-if="editItemId === item.id" class="border rounded p-2 mt-2 bg-body-tertiary">
                <BRow class="g-2 mb-2">
                  <BCol><BFormInput v-model="editItem.purchasedQuantity" type="number" step="0.01" placeholder="Gekauft" /></BCol>
                  <BCol><BFormInput v-model="editItem.actualTotalPrice" type="number" step="0.01" placeholder="Tatsächlicher Preis" /></BCol>
                </BRow>
                <div class="d-flex gap-3 align-items-center flex-wrap">
                  <BFormCheckbox v-model="editItem.isChecked">Bereits erledigt</BFormCheckbox>
                  <BButton size="sm" variant="primary" @click="updateItem(item)">Speichern</BButton>
                  <BButton size="sm" variant="outline-secondary" @click="cancelEditItem">Abbrechen</BButton>
                </div>
              </div>

              <div v-if="expandedPriceItemIds.has(item.id)" class="border rounded p-2 mt-2 bg-body-tertiary">
                <h3 class="h6">Preisoptionen</h3>

                <BTableSimple v-if="item.priceOptions.length > 0" small responsive class="align-middle">
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
                        <td><BFormInput v-model="editPriceOption.storeName" type="text" /></td>
                        <td><BFormInput v-model="editPriceOption.productName" type="text" /></td>
                        <td><BFormInput v-model="editPriceOption.unitPrice" type="number" step="0.01" /></td>
                        <td><BFormInput v-model="editPriceOption.totalPrice" type="number" step="0.01" /></td>
                        <td><BFormCheckbox v-model="editPriceOption.isAvailable" /></td>
                        <td><BFormInput v-model="editPriceOption.productUrl" type="text" /></td>
                        <td>
                          <div class="d-flex gap-2">
                            <BButton size="sm" variant="primary" @click="updatePriceOption(item, priceOption.id)">Speichern</BButton>
                            <BButton size="sm" variant="outline-secondary" @click="cancelEditPriceOption">Abbrechen</BButton>
                          </div>
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
                        <td>
                          <div class="d-flex gap-2">
                            <BButton size="sm" variant="outline-secondary" @click="startEditPriceOption(priceOption)">Bearbeiten</BButton>
                            <BButton size="sm" variant="outline-danger" @click="deletePriceOption(item, priceOption.id)">Löschen</BButton>
                          </div>
                        </td>
                      </template>
                    </tr>
                  </tbody>
                </BTableSimple>

                <p v-else class="text-secondary small">Noch keine Preisoptionen vorhanden.</p>

                <div class="border-top pt-2 mt-2">
                  <div class="text-secondary small mb-2">Preisoption hinzufügen</div>
                  <BRow class="g-2 mb-2">
                    <BCol md="6"><BFormInput v-model="getNewPriceOptionState(item).storeName" type="text" placeholder="Händler" /></BCol>
                    <BCol md="6"><BFormInput v-model="getNewPriceOptionState(item).productName" type="text" placeholder="Produktname" /></BCol>
                    <BCol md="6"><BFormInput v-model="getNewPriceOptionState(item).unitPrice" type="number" step="0.01" placeholder="Einzelpreis" /></BCol>
                    <BCol md="6"><BFormInput v-model="getNewPriceOptionState(item).totalPrice" type="number" step="0.01" placeholder="Gesamtpreis" /></BCol>
                    <BCol cols="12"><BFormInput v-model="getNewPriceOptionState(item).productUrl" type="text" placeholder="Produkt-URL" /></BCol>
                  </BRow>
                  <div class="d-flex gap-3 align-items-center flex-wrap">
                    <BFormCheckbox v-model="getNewPriceOptionState(item).isAvailable">Verfügbar</BFormCheckbox>
                    <BButton size="sm" variant="primary" @click="createPriceOption(item)">Preisoption speichern</BButton>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </BCard>

        <BCard v-else>
          <p class="text-secondary text-center mb-0">Wähle links eine Einkaufsliste aus, um den Einkaufszettel zu sehen.</p>
        </BCard>
      </BCol>
    </BRow>

    <input
      ref="receiptFileInput"
      type="file"
      accept="image/*"
      capture="environment"
      style="display: none"
      @change="onReceiptFileSelected"
    />

    <BModal v-model="showReceiptModal" title="📷 Beleg prüfen" size="lg" hide-footer @hide="closeReceiptModal">
      <div v-if="receiptResult">
        <div class="d-flex gap-3 flex-wrap mb-3">
          <span><strong>{{ receiptResult.storeName ?? 'Unbekanntes Geschäft' }}</strong></span>
          <span v-if="receiptResult.purchaseDate">{{ receiptResult.purchaseDate }}</span>
          <span v-if="receiptResult.totalAmount != null">Bon-Summe: {{ formatMoney(receiptResult.totalAmount) }}</span>
        </div>

        <BTableSimple small responsive class="align-middle">
          <thead>
            <tr>
              <th>Artikel laut Bon</th>
              <th>Menge</th>
              <th>Preis (€)</th>
              <th>Zuordnung Einkaufsliste</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(line, index) in receiptResult.lines" :key="index">
              <td><BFormInput v-model="line.name" type="text" /></td>
              <td><BFormInput v-model.number="line.quantity" type="number" min="0" step="0.01" /></td>
              <td><BFormInput v-model.number="line.totalPrice" type="number" min="0" step="0.01" /></td>
              <td>
                <BFormSelect v-model="line.shoppingItemId">
                  <option :value="null">— nicht zuordnen —</option>
                  <option v-for="item in receiptShoppingItems" :key="item.id" :value="item.id">{{ item.name }}</option>
                </BFormSelect>
              </td>
              <td><BButton size="sm" variant="outline-danger" title="Position entfernen" @click="removeReceiptLine(index)">✕</BButton></td>
            </tr>
          </tbody>
        </BTableSimple>
        <p class="text-secondary small">Summe der Positionen: {{ formatMoney(receiptLinesTotal) }}</p>

        <div class="d-flex flex-column gap-2 border-top pt-3">
          <BFormCheckbox v-model="receiptCompleteList">Liste abschließen (gekaufte Artikel ins Inventar übernehmen)</BFormCheckbox>
          <BFormCheckbox v-if="receiptCompleteList" v-model="completeAsBudgetExpense[receiptListId]">Als Budget-Ausgabe buchen</BFormCheckbox>
          <BFormSelect
            v-if="receiptCompleteList && completeAsBudgetExpense[receiptListId]"
            v-model="selectedExpenseCategoryId[receiptListId]"
          >
            <option value="">Budget-Kategorie wählen</option>
            <option v-for="category in expenseCategories" :key="category.id" :value="category.id">{{ category.name }}</option>
          </BFormSelect>

          <div class="d-flex gap-2 justify-content-end">
            <BButton variant="outline-secondary" :disabled="receiptApplying" @click="closeReceiptModal">Abbrechen</BButton>
            <BButton variant="primary" :disabled="receiptApplying" @click="applyReceipt">
              {{ receiptApplying ? 'Übernehme…' : 'Übernehmen' }}
            </BButton>
          </div>
        </div>
      </div>
    </BModal>
  </BContainer>
</template>

<style scoped>
/* Erledigte Artikel optisch abschwächen + durchstreichen. */
.checked {
  opacity: 0.6;
}
.checked .form-control,
.checked .fw-semibold {
  text-decoration: line-through;
}
</style>
