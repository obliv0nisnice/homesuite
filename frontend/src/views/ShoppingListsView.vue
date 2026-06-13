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
const syncStatusClass = computed(() => {
  if (isSyncing.value) {
    return 'sync-chip-syncing'
  }

  if (!isOnline.value) {
    return pendingMutationCount.value > 0 ? 'sync-chip-pending' : 'sync-chip-offline'
  }

  if (pendingMutationCount.value > 0) {
    return 'sync-chip-pending'
  }

  return 'sync-chip-online'
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
  <div class="dashboard-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Einkaufsliste <span class="title-accent">Marktzettel</span></h1>
        <p class="page-subtitle">Charmant wie ein echter Einkaufszettel, aber mit Preisen, Quellen und Inventar-Abgleich.</p>
      </div>
      <div class="header-actions">
        <span :class="['sync-chip', syncStatusClass]">{{ syncStatusLabel }}</span>
      </div>
    </div>

    <div v-if="error" class="alert alert-error">{{ error }}</div>
    <div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="pendingMutationCount > 0" class="alert">
      {{ pendingMutationLabel }}
    </div>
    <div v-if="offlineSnapshotAt" class="alert">
      Offline-Snapshot von {{ offlineSnapshotLabel }}
    </div>
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

          <div v-if="shoppingLists.length > 0" class="list-cards">
            <div
              v-for="list in shoppingLists"
              :key="list.id"
              class="list-card"
              :class="{ selected: selectedListId === list.id }"
            >
              <template v-if="editListId === list.id">
                <div class="form-grid full">
                  <input v-model="editList.name" type="text" />
                </div>
                <div class="actions">
                  <button class="btn-save" @click="updateList(list.id)">Speichern</button>
                  <button class="btn-secondary" @click="cancelEditList">Abbrechen</button>
                </div>
              </template>
              <template v-else>
                <div class="list-card-top clickable" @click="selectedListId = list.id">
                  <div class="list-card-headline">
                    <span class="list-card-name">{{ list.name }}</span>
                    <span class="list-card-date">{{ new Date(list.createdAt).toLocaleDateString('de-AT') }}</span>
                  </div>
                  <span class="badge badge-primary">{{ list.items.length }} Artikel</span>
                </div>

                <div class="list-card-meta">
                  <span class="meta-pill">
                    Geschätzt
                    <strong>{{ formatMoney(list.items.reduce((sum, item) => sum + (item.estimatedTotalPrice ?? 0), 0)) }}</strong>
                  </span>
                  <span class="meta-pill">
                    Tatsächlich
                    <strong>{{ formatMoney(list.items.reduce((sum, item) => sum + (item.actualTotalPrice ?? 0), 0)) }}</strong>
                  </span>
                </div>

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

                  <p
                    v-if="completeAsBudgetExpense[list.id] && budgetHintByListId[list.id]"
                    class="budget-hint"
                    :class="{ 'budget-hint-over': budgetHintByListId[list.id]!.over }"
                  >
                    {{ budgetHintByListId[list.id]!.text }}
                  </p>
                </div>

                <div class="actions list-card-actions">
                  <button class="btn-add" @click="selectedListId = list.id">Öffnen</button>
                  <button
                    class="btn-secondary"
                    @click="startReceiptScan(list.id)"
                    :disabled="receiptScanning"
                  >
                    {{ receiptScanning && receiptListId === list.id ? '⏳ Scanne…' : '📷 Bon scannen' }}
                  </button>
                  <button class="btn-secondary" @click="startEditList(list)">Bearbeiten</button>
                  <button class="btn-secondary" @click="completeShoppingList(list.id)">Abschließen</button>
                  <button class="btn-danger" @click="deleteList(list.id)">Löschen</button>
                </div>
              </template>
            </div>
          </div>

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
              <input
                v-model="newItem.name"
                list="shopping-catalog-name-suggestions"
                type="text"
                placeholder="Name"
                required
                @input="applyCatalogNameSuggestion"
              />
              <input v-model="newItem.requiredQuantity" type="number" min="0" step="0.01" placeholder="Benötigt" required />
              <input v-model="newItem.unit" type="text" placeholder="Einheit" required />
            </div>
            <datalist id="shopping-catalog-name-suggestions">
              <option
                v-for="catalogItem in catalogNameSuggestions"
                :key="catalogItem.id"
                :value="catalogItem.name"
                :label="`${catalogItem.name} · ${catalogItem.defaultUnit}`"
              />
            </datalist>
            <div v-if="selectedCatalogEstimate" class="catalog-price-hint">
              <span>Vorschlag aus Katalog:</span>
              <strong>{{ selectedCatalogEstimate.storeName }}</strong>
              <span>· {{ formatMoney(selectedCatalogEstimate.estimatedTotalPrice) }}</span>
            </div>
            <div class="form-actions">
              <button class="btn-add" type="submit">Artikel hinzufügen</button>
            </div>
          </form>
        </div>

        <div v-if="selectedList" class="sheet-card notebook-card">
          <div class="sheet-header">
            <div>
              <h2 class="card-title">Notizblock</h2>
              <p class="card-copy">Eine Zeile = ein Artikel. Tippen oder auf dem iPad mit Apple Pencil schreiben. Häkchen zum Abhaken; leere Zeile + Entfernen löscht den Artikel.</p>
            </div>
          </div>

          <p class="match-mode-note">
            Zuordnung: <strong>lokal im Browser</strong>
            <button type="button" class="link-btn" @click="toggleMatchingMode">
              {{ matchingMode === 'client' ? 'auf KI umstellen' : 'auf lokal zurück' }}
            </button>
            <span v-if="matchingMode === 'llm'"> · KI-Zuordnung ist vorbereitet, aber noch nicht aktiv – es wird weiter lokal zugeordnet.</span>
          </p>

          <div class="notebook-list">
            <div
              v-for="item in selectedList.items"
              :key="item.id"
              class="notebook-row"
              :class="{ checked: item.isChecked }"
            >
              <label class="grocery-check">
                <input :checked="item.isChecked" type="checkbox" @change="toggleItem(item)" />
                <span class="grocery-check-mark"></span>
              </label>
              <input
                class="notebook-input"
                :value="item.name"
                type="text"
                enterkeyhint="next"
                @keydown.enter.prevent="commitItemName(item, ($event.target as HTMLInputElement).value)"
                @blur="commitItemName(item, ($event.target as HTMLInputElement).value)"
                @keydown.delete="onNotebookBackspace(item, $event)"
              />
              <span class="notebook-amount">{{ item.quantity }} {{ item.unit }}</span>
              <span v-if="item.catalogItemName" class="notebook-badge">{{ item.catalogItemName }}</span>
              <span v-else class="notebook-badge notebook-badge-new">neu</span>
            </div>

            <div class="notebook-row notebook-row-new">
              <span class="notebook-bullet">+</span>
              <input
                class="notebook-input"
                v-model="newLineText"
                type="text"
                placeholder="Artikel schreiben …"
                enterkeyhint="done"
                @keydown.enter.prevent="commitNewLine"
              />
            </div>
          </div>
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

        <div v-if="bestCompleteStoreSummary" class="catalog-price-hint">
          <span>Günstigster vollständiger Händler:</span>
          <strong>{{ bestCompleteStoreSummary.storeName }}</strong>
          <span>· {{ formatMoney(bestCompleteStoreSummary.totalEstimatedPrice) }}</span>
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
              <div class="grocery-text">
                <div class="grocery-headline">
                  <div class="grocery-name">{{ item.name }}</div>
                  <div class="grocery-amount">{{ item.quantity }} {{ item.unit }}</div>
                </div>
              </div>

              <label class="grocery-check">
                <input :checked="item.isChecked" type="checkbox" @change="toggleItem(item)" />
                <span class="grocery-check-mark"></span>
              </label>
            </div>

            <div class="grocery-footer">
              <div class="grocery-prices">
                <span class="price-pill">
                  Geschätzt <strong>{{ formatMoney(item.estimatedTotalPrice) }}</strong>
                </span>
                <span class="price-pill price-pill-actual">
                  Echt <strong>{{ formatMoney(item.actualTotalPrice) }}</strong>
                </span>
              </div>

              <div class="grocery-meta">
                <span>{{ displaySource(item.sourceType) }}</span>
                <span v-if="item.catalogItemName">· {{ item.catalogItemName }}</span>
                <span>· Bedarf {{ item.requiredQuantity }} {{ item.unit }}</span>
                <span>· Inventar {{ item.inventoryQuantityUsed }}</span>
              </div>

              <div class="actions grocery-actions">
                <button class="btn-secondary" @click="startEditItem(item)">Echten Einkauf pflegen</button>
                <button class="btn-secondary" @click="togglePriceOptions(item.id)">
                  {{ expandedPriceItemIds.has(item.id) ? '▾' : '▸' }} Preisoptionen ({{ item.priceOptions.length }})
                </button>
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

            <div v-if="expandedPriceItemIds.has(item.id)" class="price-board">
              <div class="card-header">
                <h3 class="card-title" style="font-size:16px;">Preisoptionen</h3>
              </div>

              <div v-if="item.priceOptions.length > 0" class="table-scroll">
              <table class="paper-table">
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
              </div>

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

      <div v-else class="sheet-card">
        <div class="empty-state">Wähle links eine Einkaufsliste aus, um den Einkaufszettel zu sehen.</div>
      </div>
    </div>

    <input
      ref="receiptFileInput"
      type="file"
      accept="image/*"
      capture="environment"
      style="display: none"
      @change="onReceiptFileSelected"
    />

    <Teleport to="body">
      <div
        v-if="showReceiptModal && receiptResult"
        class="receipt-modal-backdrop"
        @click.self="closeReceiptModal"
      >
        <div class="receipt-modal">
          <div class="receipt-modal-header">
            <h2>📷 Beleg prüfen</h2>
            <button class="receipt-modal-close" @click="closeReceiptModal">✕</button>
          </div>

          <div class="receipt-modal-meta">
            <span><strong>{{ receiptResult.storeName ?? 'Unbekanntes Geschäft' }}</strong></span>
            <span v-if="receiptResult.purchaseDate">{{ receiptResult.purchaseDate }}</span>
            <span v-if="receiptResult.totalAmount != null">
              Bon-Summe: {{ formatMoney(receiptResult.totalAmount) }}
            </span>
          </div>

          <div class="table-scroll">
          <table class="receipt-table">
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
                <td><input v-model="line.name" type="text" /></td>
                <td><input v-model.number="line.quantity" type="number" min="0" step="0.01" class="receipt-num" /></td>
                <td><input v-model.number="line.totalPrice" type="number" min="0" step="0.01" class="receipt-num" /></td>
                <td>
                  <select v-model="line.shoppingItemId">
                    <option :value="null">— nicht zuordnen —</option>
                    <option v-for="item in receiptShoppingItems" :key="item.id" :value="item.id">
                      {{ item.name }}
                    </option>
                  </select>
                </td>
                <td>
                  <button class="btn-danger" @click="removeReceiptLine(index)" title="Position entfernen">✕</button>
                </td>
              </tr>
            </tbody>
          </table>
          </div>
          <p class="receipt-sum">Summe der Positionen: {{ formatMoney(receiptLinesTotal) }}</p>

          <div class="receipt-modal-footer">
            <label class="checkbox-row">
              <input v-model="receiptCompleteList" type="checkbox" />
              <span>Liste abschließen (gekaufte Artikel ins Inventar übernehmen)</span>
            </label>

            <label v-if="receiptCompleteList" class="checkbox-row">
              <input v-model="completeAsBudgetExpense[receiptListId]" type="checkbox" />
              <span>Als Budget-Ausgabe buchen</span>
            </label>

            <select
              v-if="receiptCompleteList && completeAsBudgetExpense[receiptListId]"
              v-model="selectedExpenseCategoryId[receiptListId]"
              class="budget-category-select"
            >
              <option value="">Budget-Kategorie wählen</option>
              <option v-for="category in expenseCategories" :key="category.id" :value="category.id">
                {{ category.name }}
              </option>
            </select>

            <div class="receipt-actions">
              <button class="btn-secondary" @click="closeReceiptModal" :disabled="receiptApplying">Abbrechen</button>
              <button class="btn-add" @click="applyReceipt" :disabled="receiptApplying">
                {{ receiptApplying ? 'Übernehme…' : 'Übernehmen' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
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

.sync-chip {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  border: 1px solid transparent;
}

.sync-chip-online {
  background: rgba(16,185,129,0.12);
  color: #10b981;
  border-color: rgba(16,185,129,0.22);
}

.sync-chip-offline {
  background: rgba(107,114,128,0.12);
  color: #6b7280;
  border-color: rgba(107,114,128,0.2);
}

.sync-chip-pending {
  background: rgba(245,158,11,0.12);
  color: #d97706;
  border-color: rgba(245,158,11,0.22);
}

.sync-chip-syncing {
  background: rgba(59,130,246,0.12);
  color: #2563eb;
  border-color: rgba(59,130,246,0.22);
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

/* Listenübersicht als Karten statt 7-Spalten-Tabelle: passt in die schmale
   Spalte und bleibt am Handy bedienbar. */
.list-cards {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.list-card {
  background: var(--surface2);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.list-card.selected {
  border-color: var(--primary);
  box-shadow: 0 0 0 2px rgba(99, 102, 241, 0.15);
}

.list-card-top {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
}

.list-card-headline {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.list-card-name {
  font-size: 17px;
  font-weight: 700;
  color: var(--text);
  overflow-wrap: anywhere;
}

.list-card-date {
  font-size: 12px;
  color: var(--text-muted);
}

.list-card-meta {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.meta-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 999px;
  padding: 5px 11px;
  font-size: 12px;
  color: var(--text-muted);
}

.meta-pill strong { color: var(--text); }

.list-card-actions button { flex: 1 1 auto; }

.table-scroll {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}

.table-scroll > table { min-width: 640px; }

.grocery-sheet {
  position: relative;
  background:
    radial-gradient(circle at top left, rgba(239, 68, 68, 0.08), transparent 18%),
    linear-gradient(to bottom, rgba(255,255,255,0.98), rgba(249,244,230,0.98)),
    repeating-linear-gradient(to bottom, transparent 0, transparent 36px, rgba(74, 144, 226, 0.14) 37px, transparent 38px);
  border: 1px solid rgba(120, 93, 51, 0.16);
  box-shadow:
    0 24px 60px rgba(64, 43, 10, 0.12),
    inset 0 1px 0 rgba(255,255,255,0.9);
}

.grocery-sheet::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 36px;
  width: 2px;
  background: rgba(220, 38, 38, 0.18);
  pointer-events: none;
}

.grocery-sheet::after {
  content: 'Einkaufszettel';
  position: absolute;
  top: 14px;
  right: 20px;
  color: rgba(120, 93, 51, 0.55);
  font-size: 14px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
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
  background: rgba(255,252,245,0.96);
  border: 1px dashed rgba(120, 93, 51, 0.24);
  border-radius: 12px;
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
  position: relative;
  margin-left: 26px;
  background: rgba(255,252,245,0.94);
  border: 1px solid rgba(120, 93, 51, 0.14);
  border-radius: 10px;
  padding: 14px 16px 12px;
  box-shadow: 0 3px 10px rgba(64, 43, 10, 0.06);
}

.grocery-row::before {
  content: '';
  position: absolute;
  left: -22px;
  top: 18px;
  width: 10px;
  height: 10px;
  border-radius: 999px;
  background: rgba(99,102,241,0.20);
  border: 1px solid rgba(99,102,241,0.25);
}

.grocery-row.checked {
  opacity: 0.75;
  filter: saturate(0.75);
}

.grocery-main {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}
@media (max-width: 860px) {
  .grocery-main {
    align-items: flex-start;
  }
}

.grocery-text {
  min-width: 0;
  flex: 1;
}

.grocery-headline {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.grocery-name {
  font-size: 20px;
  font-weight: 700;
  color: var(--text);
  margin: 0;
  font-family: "Bradley Hand", "Segoe Print", "Comic Sans MS", cursive;
}

.grocery-amount {
  display: inline-flex;
  align-items: center;
  padding: 7px 12px;
  border-radius: 999px;
  background: rgba(120, 93, 51, 0.08);
  border: 1px solid rgba(120, 93, 51, 0.18);
  color: var(--text);
  font-size: 14px;
  font-weight: 800;
  white-space: nowrap;
}

.grocery-check {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  flex-shrink: 0;
}

.grocery-check input {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
  margin: 0;
}

.grocery-check-mark {
  width: 28px;
  height: 28px;
  border-radius: 10px;
  border: 2px solid rgba(120, 93, 51, 0.35);
  background: rgba(255,255,255,0.7);
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.9);
  transition: transform 0.15s ease, background 0.2s ease, border-color 0.2s ease;
}

.grocery-check input:checked + .grocery-check-mark {
  background: var(--primary);
  border-color: var(--primary);
  transform: scale(1.02);
}

.grocery-check input:checked + .grocery-check-mark::after {
  content: '';
  display: block;
  width: 7px;
  height: 13px;
  margin: 4px 0 0 9px;
  border: solid white;
  border-width: 0 3px 3px 0;
  transform: rotate(45deg);
}

.grocery-footer {
  margin-top: 10px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.grocery-row.checked .grocery-name,
.grocery-row.checked .grocery-amount {
  text-decoration: line-through;
  text-decoration-thickness: 2px;
  text-decoration-color: rgba(120, 93, 51, 0.75);
}

.grocery-meta {
  color: var(--text-muted);
  font-size: 12px;
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.grocery-prices {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.price-pill {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 999px;
  background: rgba(120, 93, 51, 0.08);
  border: 1px solid rgba(120, 93, 51, 0.16);
  color: var(--text-muted);
  font-size: 13px;
}

.price-pill strong {
  color: var(--text);
}

.price-pill-actual {
  background: rgba(16,185,129,0.08);
  border-color: rgba(16,185,129,0.18);
}

.grocery-actions {
  padding-top: 4px;
}

.inline-editor,
.price-board,
.store-compare {
  margin-top: 14px;
  background: rgba(255,250,240,0.88);
  border: 1px dashed rgba(120, 93, 51, 0.24);
  border-radius: 14px;
  padding: 14px;
}

.catalog-price-hint {
  margin-top: 12px;
  display: inline-flex;
  gap: 8px;
  flex-wrap: wrap;
  align-items: center;
  padding: 8px 12px;
  border-radius: 12px;
  background: rgba(245, 158, 11, 0.10);
  border: 1px dashed rgba(245, 158, 11, 0.24);
  color: var(--text-muted);
  font-size: 13px;
}

:global(.dark-mode) .grocery-sheet {
  background:
    radial-gradient(circle at top left, rgba(96, 165, 250, 0.12), transparent 22%),
    linear-gradient(to bottom, rgba(18,24,38,0.98), rgba(15,23,42,0.98)),
    repeating-linear-gradient(to bottom, transparent 0, transparent 36px, rgba(96, 165, 250, 0.12) 37px, transparent 38px);
  border-color: rgba(96, 165, 250, 0.16);
  box-shadow:
    0 24px 60px rgba(2, 6, 23, 0.46),
    inset 0 1px 0 rgba(255,255,255,0.03);
}

:global(.dark-mode) .grocery-sheet::before {
  background: rgba(248, 113, 113, 0.22);
}

:global(.dark-mode) .grocery-sheet::after {
  color: rgba(148, 163, 184, 0.7);
}

:global(.dark-mode) .grocery-row {
  background: rgba(15, 23, 42, 0.88);
  border-color: rgba(148, 163, 184, 0.18);
  box-shadow: 0 10px 24px rgba(2, 6, 23, 0.28);
}

:global(.dark-mode) .grocery-row::before {
  background: rgba(96, 165, 250, 0.28);
  border-color: rgba(96, 165, 250, 0.38);
}

:global(.dark-mode) .grocery-amount,
:global(.dark-mode) .price-pill {
  background: rgba(30, 41, 59, 0.92);
  border-color: rgba(148, 163, 184, 0.18);
}

:global(.dark-mode) .price-pill-actual {
  background: rgba(6, 78, 59, 0.35);
  border-color: rgba(16,185,129,0.24);
}

:global(.dark-mode) .grocery-check-mark {
  background: rgba(30, 41, 59, 0.95);
  border-color: rgba(148, 163, 184, 0.3);
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.04);
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
.budget-hint { margin: 6px 0 0; font-size: 12px; font-weight: 600; color: #10b981; }
.budget-hint-over { color: #ef4444; }

/* Beleg-Scan-Modal */
.receipt-modal-backdrop { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.5); display: flex; align-items: center; justify-content: center; z-index: 999; padding: 16px; backdrop-filter: blur(4px); }
.receipt-modal { background: var(--surface); border: 1px solid var(--border); border-radius: 14px; width: 100%; max-width: 780px; max-height: 92vh; overflow-y: auto; padding: 20px; display: flex; flex-direction: column; gap: 14px; }
.receipt-modal-header { display: flex; justify-content: space-between; align-items: center; }
.receipt-modal-header h2 { font-size: 17px; font-weight: 700; color: var(--text); margin: 0; }
.receipt-modal-close { background: none; border: none; font-size: 16px; cursor: pointer; color: var(--text-muted); padding: 4px 8px; border-radius: 6px; }
.receipt-modal-close:hover { background: var(--surface2); }
.receipt-modal-meta { display: flex; gap: 16px; flex-wrap: wrap; font-size: 13px; color: var(--text); }
.receipt-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.receipt-table th { text-align: left; padding: 6px 4px; color: var(--text-muted); font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
.receipt-table td { padding: 4px; border-top: 1px solid var(--border); }
.receipt-table input, .receipt-table select { width: 100%; padding: 6px 8px; border: 1px solid var(--border); border-radius: 8px; background: var(--surface2); color: var(--text); font-size: 13px; box-sizing: border-box; }
.receipt-num { max-width: 90px; }
.receipt-sum { font-size: 13px; font-weight: 600; color: var(--text); margin: 0; text-align: right; }
.receipt-modal-footer { display: flex; flex-direction: column; gap: 10px; border-top: 1px solid var(--border); padding-top: 12px; }
.receipt-actions { display: flex; justify-content: flex-end; gap: 10px; }

/* Notizblock-Eingabe */
.notebook-card { gap: 14px; }
.match-mode-note { font-size: 12px; color: var(--muted); margin: 0; display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
.link-btn { background: none; border: none; padding: 0; color: var(--accent, #2d6cdf); font: inherit; cursor: pointer; text-decoration: underline; }
.notebook-list { display: flex; flex-direction: column; }
.notebook-row { display: flex; align-items: center; gap: 10px; padding: 6px 4px; border-bottom: 1px solid var(--border); }
.notebook-row.checked .notebook-input { text-decoration: line-through; color: var(--muted); }
.notebook-input { flex: 1 1 auto; border: none; background: transparent; font-size: 15px; color: var(--text); padding: 4px 0; min-width: 0; }
.notebook-input:focus { outline: none; }
.notebook-amount { font-size: 12px; color: var(--muted); white-space: nowrap; }
.notebook-badge { font-size: 11px; padding: 2px 8px; border-radius: 999px; background: var(--surface-muted, #eef2f7); color: var(--muted); white-space: nowrap; }
.notebook-badge-new { background: #fdf0d5; color: #9a6b00; }
.notebook-row-new .notebook-bullet { width: 22px; text-align: center; color: var(--muted); font-size: 18px; }

/* Kompaktere Abstände und volle Button-Breiten am Handy */
@media (max-width: 560px) {
  .dashboard-page { padding: 16px 12px; gap: 16px; }
  .panel-card, .form-card, .data-card, .sheet-card { padding: 16px; }
  .grocery-row { margin-left: 16px; padding: 12px 12px 10px; }
  .grocery-row::before { left: -14px; }
  .grocery-sheet::before { inset: 0 auto 0 20px; }
  .actions button { flex: 1 1 auto; }
}
</style>
