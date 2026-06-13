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

type CatalogItem = {
  id: string
  name: string
  defaultUnit: string
}

const items = ref<InventoryItem[]>([])
const catalogItems = ref<CatalogItem[]>([])
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

const newItemSuggestions = computed(() => getCatalogSuggestions(newItem.value.name))
const editItemSuggestions = computed(() => getCatalogSuggestions(editItem.value.name))

function applyCatalogSuggestion(target: { name: string; unit: string }) {
  const matchingCatalogItem = catalogItems.value.find(
    (item) => item.name.toLowerCase() === target.name.trim().toLowerCase(),
  )

  if (!matchingCatalogItem) {
    return
  }

  target.name = matchingCatalogItem.name
  if (!target.unit.trim()) {
    target.unit = matchingCatalogItem.defaultUnit
  }
}

async function loadData() {
  loading.value = true
  error.value = ''
  success.value = ''

  try {
    const [inventoryData, catalogItemData] = await Promise.all([
      apiFetch<InventoryItem[]>('/inventory'),
      apiFetch<CatalogItem[]>('/catalog'),
    ])

    items.value = inventoryData
    catalogItems.value = catalogItemData
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
  <BContainer class="py-4 d-flex flex-column gap-4">
    <div>
      <h1 class="h2 fw-bold mb-1">Inventar <span class="text-primary">Vorräte</span></h1>
      <p class="text-secondary mb-0">
        Alles, was schon da ist – damit Planner und Einkaufsliste clever gegenrechnen können.
      </p>
    </div>

    <BAlert :model-value="!!error" variant="danger">{{ error }}</BAlert>
    <BAlert :model-value="!!success" variant="success">{{ success }}</BAlert>
    <BAlert :model-value="loading" variant="info">Lade Inventardaten…</BAlert>

    <BRow>
      <BCol md="3">
        <BCard>
          <div class="text-secondary text-uppercase small">Einträge</div>
          <div class="fs-3 fw-bold">{{ totalItems }}</div>
        </BCard>
      </BCol>
    </BRow>

    <BRow class="g-4">
      <BCol lg="4">
        <BCard title="Neuen Inventar-Eintrag anlegen">
          <p class="text-secondary small">
            Halte Mengen und Notizen aktuell, damit Wochenplan und Einkaufsliste sauber rechnen.
          </p>

          <BForm @submit.prevent="createItem">
            <BFormInput
              v-model="newItem.name"
              list="inventory-catalog-suggestions"
              type="text"
              placeholder="Name"
              required
              class="mb-2"
              @input="applyCatalogSuggestion(newItem)"
            />
            <BFormInput
              v-model="newItem.quantity"
              type="number"
              min="0"
              step="0.01"
              placeholder="Menge"
              required
              class="mb-2"
            />
            <BFormInput v-model="newItem.unit" type="text" placeholder="Einheit" required class="mb-2" />
            <BFormTextarea v-model="newItem.notes" placeholder="Notizen" rows="3" class="mb-3" />

            <BButton type="submit" variant="primary">Speichern</BButton>
          </BForm>

          <datalist id="inventory-catalog-suggestions">
            <option
              v-for="item in newItemSuggestions"
              :key="item.id"
              :value="item.name"
              :label="`${item.name} · ${item.defaultUnit}`"
            />
          </datalist>
        </BCard>
      </BCol>

      <BCol lg="8">
        <BCard title="Inventarübersicht">
          <p class="text-secondary small">
            Direkt bearbeitbar und bewusst schlicht gehalten – wie ein digitaler Vorratsschrank.
          </p>

          <BTableSimple v-if="items.length > 0" hover responsive class="align-middle">
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
                    <BFormInput
                      v-model="editItem.name"
                      list="inventory-edit-catalog-suggestions"
                      type="text"
                      @input="applyCatalogSuggestion(editItem)"
                    />
                  </td>
                  <td><BFormInput v-model="editItem.quantity" type="number" min="0" step="0.01" /></td>
                  <td><BFormInput v-model="editItem.unit" type="text" /></td>
                  <td><BFormTextarea v-model="editItem.notes" rows="2" /></td>
                  <td>
                    <div class="d-flex gap-2 flex-wrap">
                      <BButton size="sm" variant="primary" @click="updateItem(item.id)">Speichern</BButton>
                      <BButton size="sm" variant="outline-secondary" @click="cancelEditItem">Abbrechen</BButton>
                    </div>
                  </td>
                </template>

                <template v-else>
                  <td><strong>{{ item.name }}</strong></td>
                  <td>{{ item.quantity }}</td>
                  <td>{{ item.unit }}</td>
                  <td>{{ item.notes || '—' }}</td>
                  <td>
                    <div class="d-flex gap-2 flex-wrap">
                      <BButton size="sm" variant="outline-secondary" @click="startEditItem(item)">Bearbeiten</BButton>
                      <BButton size="sm" variant="outline-danger" @click="deleteItem(item.id)">Löschen</BButton>
                    </div>
                  </td>
                </template>
              </tr>
            </tbody>
          </BTableSimple>

          <p v-else class="text-secondary mb-0">Noch keine Inventar-Einträge vorhanden.</p>

          <datalist id="inventory-edit-catalog-suggestions">
            <option
              v-for="item in editItemSuggestions"
              :key="item.id"
              :value="item.name"
              :label="`${item.name} · ${item.defaultUnit}`"
            />
          </datalist>
        </BCard>
      </BCol>
    </BRow>
  </BContainer>
</template>
