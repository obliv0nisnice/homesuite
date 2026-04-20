<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '@/services/api'

type RecipeIngredient = {
  id: string
  name: string
  quantity: number
  unit: string
  recipeId: string
}

type Recipe = {
  id: string
  name: string
  description?: string | null
  baseServings: number
  ingredients: RecipeIngredient[]
}

type ShoppingListOption = {
  id: string
  name: string
  createdAt: string
}

const recipes = ref<Recipe[]>([])
const shoppingLists = ref<ShoppingListOption[]>([])
const selectedRecipeId = ref<string>('')
const selectedShoppingListId = ref<string>('')
const loading = ref(false)
const error = ref('')
const success = ref('')

const newRecipe = ref({
  name: '',
  description: '',
  baseServings: 1,
})

const editRecipeId = ref<string | null>(null)
const editRecipe = ref({
  name: '',
  description: '',
  baseServings: 1,
})

const newIngredient = ref({
  name: '',
  quantity: 1,
  unit: 'Stk',
})

const editIngredientId = ref<string | null>(null)
const editIngredient = ref({
  name: '',
  quantity: 1,
  unit: 'Stk',
})

const selectedRecipe = computed(() =>
  recipes.value.find((x) => x.id === selectedRecipeId.value) ?? null,
)

async function loadData() {
  loading.value = true
  error.value = ''
  success.value = ''

  try {
    const [recipeData, shoppingListData] = await Promise.all([
      apiFetch<Recipe[]>('/recipes'),
      apiFetch<ShoppingListOption[]>('/shoppinglists'),
    ])

    recipes.value = recipeData
    shoppingLists.value = shoppingListData

    if (!selectedRecipeId.value && recipeData.length > 0) {
      selectedRecipeId.value = recipeData[0]?.id ?? ''
    }

    if (selectedRecipeId.value && !recipeData.some((x) => x.id === selectedRecipeId.value)) {
      selectedRecipeId.value = recipeData[0]?.id ?? ''
    }

    if (!selectedShoppingListId.value && shoppingListData.length > 0) {
      selectedShoppingListId.value = shoppingListData[0]?.id ?? ''
    }

    if (
      selectedShoppingListId.value &&
      !shoppingListData.some((x) => x.id === selectedShoppingListId.value)
    ) {
      selectedShoppingListId.value = shoppingListData[0]?.id ?? ''
    }
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Rezepte konnten nicht geladen werden.'
  } finally {
    loading.value = false
  }
}

async function createRecipe() {
  error.value = ''
  success.value = ''

  try {
    const created = await apiFetch<Recipe>('/recipes', {
      method: 'POST',
      body: JSON.stringify({
        ...newRecipe.value,
        baseServings: Number(newRecipe.value.baseServings),
      }),
    })

    newRecipe.value = {
      name: '',
      description: '',
      baseServings: 1,
    }

    await loadData()
    selectedRecipeId.value = created.id
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Rezept konnte nicht erstellt werden.'
  }
}

function startEditRecipe(recipe: Recipe) {
  editRecipeId.value = recipe.id
  editRecipe.value = {
    name: recipe.name,
    description: recipe.description ?? '',
    baseServings: recipe.baseServings,
  }
}

function cancelEditRecipe() {
  editRecipeId.value = null
  editRecipe.value = {
    name: '',
    description: '',
    baseServings: 1,
  }
}

async function updateRecipe(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<Recipe>(`/recipes/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        ...editRecipe.value,
        baseServings: Number(editRecipe.value.baseServings),
      }),
    })

    cancelEditRecipe()
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Rezept konnte nicht aktualisiert werden.'
  }
}

async function deleteRecipe(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>(`/recipes/${id}`, {
      method: 'DELETE',
    })

    if (editRecipeId.value === id) {
      cancelEditRecipe()
    }

    if (selectedRecipeId.value === id) {
      selectedRecipeId.value = ''
    }

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Rezept konnte nicht gelöscht werden.'
  }
}

async function createIngredient() {
  if (!selectedRecipeId.value) {
    error.value = 'Bitte zuerst ein Rezept auswählen.'
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<RecipeIngredient>(`/recipes/${selectedRecipeId.value}/ingredients`, {
      method: 'POST',
      body: JSON.stringify({
        ...newIngredient.value,
        quantity: Number(newIngredient.value.quantity),
      }),
    })

    newIngredient.value = {
      name: '',
      quantity: 1,
      unit: 'Stk',
    }

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Zutat konnte nicht erstellt werden.'
  }
}

function startEditIngredient(ingredient: RecipeIngredient) {
  editIngredientId.value = ingredient.id
  editIngredient.value = {
    name: ingredient.name,
    quantity: ingredient.quantity,
    unit: ingredient.unit,
  }
}

function cancelEditIngredient() {
  editIngredientId.value = null
  editIngredient.value = {
    name: '',
    quantity: 1,
    unit: 'Stk',
  }
}

async function updateIngredient(ingredientId: string) {
  if (!selectedRecipeId.value) {
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<RecipeIngredient>(
      `/recipes/${selectedRecipeId.value}/ingredients/${ingredientId}`,
      {
        method: 'PUT',
        body: JSON.stringify({
          ...editIngredient.value,
          quantity: Number(editIngredient.value.quantity),
        }),
      },
    )

    cancelEditIngredient()
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Zutat konnte nicht aktualisiert werden.'
  }
}

async function deleteIngredient(ingredientId: string) {
  if (!selectedRecipeId.value) {
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>(`/recipes/${selectedRecipeId.value}/ingredients/${ingredientId}`, {
      method: 'DELETE',
    })

    if (editIngredientId.value === ingredientId) {
      cancelEditIngredient()
    }

    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Zutat konnte nicht gelöscht werden.'
  }
}

async function addRecipeToShoppingList() {
  if (!selectedRecipeId.value) {
    error.value = 'Bitte zuerst ein Rezept auswählen.'
    return
  }

  if (!selectedShoppingListId.value) {
    error.value = 'Bitte zuerst eine Einkaufsliste auswählen.'
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch(`/shoppinglists/${selectedShoppingListId.value}/add-recipe/${selectedRecipeId.value}`, {
      method: 'POST',
    })

    success.value = 'Rezept wurde zur Einkaufsliste hinzugefügt.'
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Rezept konnte nicht übernommen werden.'
  }
}

onMounted(loadData)
</script>


<template>
  <div class="dashboard-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Rezepte <span class="title-accent">Kochbuch</span></h1>
        <p class="page-subtitle">Rezepte pflegen, Zutaten strukturieren und direkt in Einkaufslisten überführen.</p>
      </div>
    </div>

    <div v-if="error" class="alert alert-error">{{ error }}</div>
    <div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="alert">Lade Rezeptdaten…</div>

    <div class="content-grid">
      <div class="stack">
        <div class="form-card">
          <div class="card-header">
            <div>
              <h2 class="card-title">Neues Rezept</h2>
              <p class="card-copy">Name, Beschreibung und Basis-Portionen als saubere Grundlage.</p>
            </div>
          </div>

          <form @submit.prevent="createRecipe">
            <div class="form-grid full">
              <input v-model="newRecipe.name" type="text" placeholder="Rezeptname" required />
              <input v-model="newRecipe.baseServings" type="number" min="1" step="1" placeholder="Basis-Portionen" required />
              <textarea v-model="newRecipe.description" placeholder="Beschreibung"></textarea>
            </div>
            <div class="form-actions">
              <button class="btn-add" type="submit">Rezept speichern</button>
            </div>
          </form>
        </div>

        <div class="form-card">
          <div class="card-header">
            <div>
              <h2 class="card-title">Rezept in Einkaufsliste übernehmen</h2>
              <p class="card-copy">Ein einzelnes Rezept gezielt auf eine bestehende Liste setzen.</p>
            </div>
          </div>

          <div class="form-grid full">
            <select v-model="selectedRecipeId">
              <option disabled value="">Rezept wählen</option>
              <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">{{ recipe.name }}</option>
            </select>
            <select v-model="selectedShoppingListId">
              <option disabled value="">Einkaufsliste wählen</option>
              <option v-for="list in shoppingLists" :key="list.id" :value="list.id">{{ list.name }}</option>
            </select>
          </div>

          <div class="form-actions">
            <button class="btn-add" type="button" @click="addRecipeToShoppingList">Zur Einkaufsliste hinzufügen</button>
          </div>
        </div>
      </div>

      <div class="data-card">
        <div class="card-header">
          <div>
            <h2 class="card-title">Rezeptübersicht</h2>
            <p class="card-copy">Rezept auswählen, bearbeiten und Zutaten direkt pflegen.</p>
          </div>
        </div>

        <div v-if="recipes.length > 0" class="compact-list">
          <div v-for="recipe in recipes" :key="recipe.id" class="compact-item">
            <template v-if="editRecipeId === recipe.id">
              <div class="form-grid full">
                <input v-model="editRecipe.name" type="text" />
                <input v-model="editRecipe.baseServings" type="number" min="1" step="1" />
                <textarea v-model="editRecipe.description" placeholder="Beschreibung"></textarea>
              </div>
              <div class="form-actions">
                <button class="btn-save" @click="updateRecipe(recipe.id)">Speichern</button>
                <button class="btn-secondary" @click="cancelEditRecipe">Abbrechen</button>
              </div>
            </template>

            <template v-else>
              <div class="card-header" style="margin-bottom: 8px;">
                <div>
                  <div style="font-weight:800; font-size:18px;">{{ recipe.name }}</div>
                  <div class="card-copy" v-if="recipe.description">{{ recipe.description }}</div>
                </div>
                <span class="badge badge-primary">{{ recipe.baseServings }} Portionen</span>
              </div>

              <div class="actions">
                <button class="btn-secondary" @click="selectedRecipeId = recipe.id">Auswählen</button>
                <button class="btn-secondary" @click="startEditRecipe(recipe)">Bearbeiten</button>
                <button class="btn-danger" @click="deleteRecipe(recipe.id)">Löschen</button>
              </div>
            </template>
          </div>
        </div>

        <div v-else class="empty-state">Noch keine Rezepte vorhanden.</div>
      </div>
    </div>

    <div v-if="selectedRecipe" class="data-card">
      <div class="card-header">
        <div>
          <h2 class="card-title">Zutaten für „{{ selectedRecipe.name }}“</h2>
          <p class="card-copy">Pflege die Zutaten direkt am Rezept – sauber, knapp, eindeutig.</p>
        </div>
      </div>

      <div class="form-card" style="padding: 0; border: none; box-shadow:none; background:transparent;">
        <form @submit.prevent="createIngredient">
          <div class="form-grid">
            <input v-model="newIngredient.name" type="text" placeholder="Zutat" required />
            <input v-model="newIngredient.quantity" type="number" min="0" step="0.01" placeholder="Menge" required />
            <input v-model="newIngredient.unit" type="text" placeholder="Einheit" required />
          </div>
          <div class="form-actions">
            <button class="btn-add" type="submit">Zutat hinzufügen</button>
          </div>
        </form>
      </div>

      <table v-if="selectedRecipe.ingredients.length > 0" class="data-table" style="margin-top: 18px;">
        <thead>
          <tr>
            <th>Name</th>
            <th>Menge</th>
            <th>Einheit</th>
            <th>Aktionen</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="ingredient in selectedRecipe.ingredients" :key="ingredient.id">
            <template v-if="editIngredientId === ingredient.id">
              <td><input v-model="editIngredient.name" type="text" /></td>
              <td><input v-model="editIngredient.quantity" type="number" min="0" step="0.01" /></td>
              <td><input v-model="editIngredient.unit" type="text" /></td>
              <td class="actions">
                <button class="btn-save" @click="updateIngredient(ingredient.id)">Speichern</button>
                <button class="btn-secondary" @click="cancelEditIngredient">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td>{{ ingredient.name }}</td>
              <td>{{ ingredient.quantity }}</td>
              <td>{{ ingredient.unit }}</td>
              <td class="actions">
                <button class="btn-secondary" @click="startEditIngredient(ingredient)">Bearbeiten</button>
                <button class="btn-danger" @click="deleteIngredient(ingredient.id)">Löschen</button>
              </td>
            </template>
          </tr>
        </tbody>
      </table>

      <div v-else class="empty-state" style="margin-top: 18px;">Dieses Rezept enthält noch keine Zutaten.</div>
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
