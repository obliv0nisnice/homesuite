<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '@/services/api'

type ShoppingItem = {
  id: string
  name: string
  quantity: number
  unit: string
  isChecked: boolean
  shoppingListId: string
}

type ShoppingList = {
  id: string
  name: string
  createdAt: string
  items: ShoppingItem[]
}

const shoppingLists = ref<ShoppingList[]>([])
const selectedListId = ref<string>('')
const loading = ref(false)
const error = ref('')

const newList = ref({
  name: '',
})

const editListId = ref<string | null>(null)
const editList = ref({
  name: '',
})

const newItem = ref({
  name: '',
  quantity: 1,
  unit: 'Stk',
})

const editItemId = ref<string | null>(null)
const editItem = ref({
  name: '',
  quantity: 1,
  unit: 'Stk',
  isChecked: false,
})

const selectedList = computed(() =>
  shoppingLists.value.find((x) => x.id === selectedListId.value) ?? null,
)

async function loadData() {
  loading.value = true
  error.value = ''

  try {
    const data = await apiFetch<ShoppingList[]>('/shoppinglists')
    shoppingLists.value = data

    if (!selectedListId.value && data.length > 0) {
      selectedListId.value = data[0].id
    }

    if (selectedListId.value && !data.some((x) => x.id === selectedListId.value)) {
      selectedListId.value = data[0]?.id ?? ''
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

  try {
    const created = await apiFetch<ShoppingList>('/shoppinglists', {
      method: 'POST',
      body: JSON.stringify(newList.value),
    })

    newList.value.name = ''
    await loadData()
    selectedListId.value = created.id
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

  try {
    await apiFetch<ShoppingList>(`/shoppinglists/${id}`, {
      method: 'PUT',
      body: JSON.stringify(editList.value),
    })

    cancelEditList()
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Einkaufsliste konnte nicht aktualisiert werden.'
  }
}

async function deleteList(id: string) {
  error.value = ''

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

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items`, {
      method: 'POST',
      body: JSON.stringify({
        ...newItem.value,
        quantity: Number(newItem.value.quantity),
      }),
    })

    newItem.value = {
      name: '',
      quantity: 1,
      unit: 'Stk',
    }

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Artikel konnte nicht erstellt werden.'
  }
}

function startEditItem(item: ShoppingItem) {
  editItemId.value = item.id
  editItem.value = {
    name: item.name,
    quantity: item.quantity,
    unit: item.unit,
    isChecked: item.isChecked,
  }
}

function cancelEditItem() {
  editItemId.value = null
  editItem.value = {
    name: '',
    quantity: 1,
    unit: 'Stk',
    isChecked: false,
  }
}

async function updateItem(itemId: string) {
  if (!selectedListId.value) {
    return
  }

  error.value = ''

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items/${itemId}`, {
      method: 'PUT',
      body: JSON.stringify({
        ...editItem.value,
        quantity: Number(editItem.value.quantity),
      }),
    })

    cancelEditItem()
    await loadData()
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

  try {
    await apiFetch<ShoppingItem>(`/shoppinglists/${selectedListId.value}/items/${item.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: item.name,
        quantity: item.quantity,
        unit: item.unit,
        isChecked: !item.isChecked,
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

  try {
    await apiFetch<void>(`/shoppinglists/${selectedListId.value}/items/${itemId}`, {
      method: 'DELETE',
    })

    if (editItemId.value === itemId) {
      cancelEditItem()
    }

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Artikel konnte nicht gelöscht werden.'
  }
}

onMounted(loadData)
</script>

<template>
  <section>
    <h2>Einkaufsliste</h2>

    <p v-if="loading">Lade Daten...</p>
    <p v-if="error" class="error">{{ error }}</p>

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
          <input v-model="newItem.name" type="text" placeholder="Artikelname" required />
          <input v-model="newItem.quantity" type="number" step="0.01" placeholder="Menge" required />
          <input v-model="newItem.unit" type="text" placeholder="Einheit" required />
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
              <td class="actions">
                <button @click="updateList(list.id)">Speichern</button>
                <button @click="cancelEditList">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td @click="selectedListId = list.id" class="clickable">{{ list.name }}</td>
              <td>{{ new Date(list.createdAt).toLocaleString() }}</td>
              <td>{{ list.items.length }}</td>
              <td class="actions">
                <button @click="selectedListId = list.id">Öffnen</button>
                <button @click="startEditList(list)">Bearbeiten</button>
                <button @click="deleteList(list.id)">Löschen</button>
              </td>
            </template>
          </tr>
        </tbody>
      </table>

      <p v-else>Noch keine Einkaufslisten vorhanden.</p>
    </div>

    <div class="card" v-if="selectedList">
      <h3>Artikel in „{{ selectedList.name }}“</h3>

      <table v-if="selectedList.items.length > 0">
        <thead>
          <tr>
            <th>Erledigt</th>
            <th>Name</th>
            <th>Menge</th>
            <th>Einheit</th>
            <th>Aktionen</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in selectedList.items" :key="item.id">
            <template v-if="editItemId === item.id">
              <td>
                <input v-model="editItem.isChecked" type="checkbox" />
              </td>
              <td>
                <input v-model="editItem.name" type="text" />
              </td>
              <td>
                <input v-model="editItem.quantity" type="number" step="0.01" />
              </td>
              <td>
                <input v-model="editItem.unit" type="text" />
              </td>
              <td class="actions">
                <button @click="updateItem(item.id)">Speichern</button>
                <button @click="cancelEditItem">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td>
                <input :checked="item.isChecked" type="checkbox" @change="toggleItem(item)" />
              </td>
              <td :class="{ checked: item.isChecked }">{{ item.name }}</td>
              <td>{{ item.quantity }}</td>
              <td>{{ item.unit }}</td>
              <td class="actions">
                <button @click="startEditItem(item)">Bearbeiten</button>
                <button @click="deleteItem(item.id)">Löschen</button>
              </td>
            </template>
          </tr>
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
button {
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

@media (max-width: 900px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
