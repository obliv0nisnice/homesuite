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

const items = ref<CatalogItem[]>([])
const loading = ref(false)
const error = ref('')
const success = ref('')

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

async function loadData() {
  loading.value = true
  error.value = ''
  success.value = ''

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
      body: JSON.stringify(newItem.value),
    })

    newItem.value = {
      name: '',
      defaultUnit: 'Stk',
      category: '',
      searchTerm: '',
      brandHint: '',
      isStaple: true,
    }

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

async function updateItem(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<CatalogItem>(`/catalog/${id}`, {
      method: 'PUT',
      body: JSON.stringify(editItem.value),
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

  try {
    await apiFetch<void>(`/catalog/${id}`, {
      method: 'DELETE',
    })

    if (editingItemId.value === id) {
      cancelEditItem()
    }

    success.value = 'Katalogeintrag wurde gelöscht.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Katalogeintrag konnte nicht gelöscht werden.'
  }
}

onMounted(loadData)
</script>

<template>
  <section>
    <h2>Katalog</h2>

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
    </div>

    <div class="card">
      <h3>Neuen Katalogeintrag anlegen</h3>

      <form class="form" @submit.prevent="createItem">
        <input v-model="newItem.name" type="text" placeholder="Name" required />
        <input v-model="newItem.defaultUnit" type="text" placeholder="Standard-Einheit" required />
        <input v-model="newItem.category" type="text" placeholder="Kategorie" />
        <input v-model="newItem.searchTerm" type="text" placeholder="Crawler-Suchbegriff" />
        <input v-model="newItem.brandHint" type="text" placeholder="Markenhinweis" />

        <label class="checkbox-row">
          <input v-model="newItem.isStaple" type="checkbox" />
          Grundartikel
        </label>

        <button type="submit">Speichern</button>
      </form>
    </div>

    <div class="card">
      <h3>Katalogeinträge</h3>

      <table v-if="items.length > 0">
        <thead>
          <tr>
            <th>Name</th>
            <th>Einheit</th>
            <th>Kategorie</th>
            <th>Suchbegriff</th>
            <th>Marke</th>
            <th>Grundartikel</th>
            <th>Aktionen</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in items" :key="item.id">
            <template v-if="editingItemId === item.id">
              <td><input v-model="editItem.name" type="text" /></td>
              <td><input v-model="editItem.defaultUnit" type="text" /></td>
              <td><input v-model="editItem.category" type="text" /></td>
              <td><input v-model="editItem.searchTerm" type="text" /></td>
              <td><input v-model="editItem.brandHint" type="text" /></td>
              <td>
                <input v-model="editItem.isStaple" type="checkbox" />
              </td>
              <td class="actions">
                <button @click="updateItem(item.id)">Speichern</button>
                <button @click="cancelEditItem">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td>{{ item.name }}</td>
              <td>{{ item.defaultUnit }}</td>
              <td>{{ item.category || '—' }}</td>
              <td>{{ item.searchTerm || '—' }}</td>
              <td>{{ item.brandHint || '—' }}</td>
              <td>{{ item.isStaple ? 'Ja' : 'Nein' }}</td>
              <td class="actions">
                <button @click="startEditItem(item)">Bearbeiten</button>
                <button @click="deleteItem(item.id)">Löschen</button>
              </td>
            </template>
          </tr>
        </tbody>
      </table>

      <p v-else>Noch keine Katalogeinträge vorhanden.</p>
    </div>
  </section>
</template>

<style scoped>
.summary-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
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

@media (max-width: 900px) {
  .summary-grid {
    grid-template-columns: 1fr;
  }
}
</style>
