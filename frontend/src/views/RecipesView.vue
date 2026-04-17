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
      selectedRecipeId.value = recipeData[0].id
    }

    if (selectedRecipeId.value && !recipeData.some((x) => x.id === selectedRecipeId.value)) {
      selectedRecipeId.value = recipeData[0]?.id ?? ''
    }

    if (!selectedShoppingListId.value && shoppingListData.length > 0) {
      selectedShoppingListId.value = shoppingListData[0].id
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
  <section>
    <h2>Rezepte</h2>

    <p v-if="loading">Lade Daten...</p>
    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="success" class="success">{{ success }}</p>

    <div class="grid">
      <div class="card">
        <h3>Neues Rezept</h3>

        <form class="form" @submit.prevent="createRecipe">
          <input v-model="newRecipe.name" type="text" placeholder="Rezeptname" required />
          <input
            v-model="newRecipe.baseServings"
            type="number"
            min="1"
            step="1"
            placeholder="Basis-Portionen"
            required
          />
          <textarea v-model="newRecipe.description" placeholder="Beschreibung"></textarea>
          <button type="submit">Speichern</button>
        </form>
      </div>

      <div class="card">
        <h3>Zutat hinzufügen</h3>

        <form class="form" @submit.prevent="createIngredient">
          <input v-model="newIngredient.name" type="text" placeholder="Zutat" required />
          <input v-model="newIngredient.quantity" type="number" step="0.01" placeholder="Menge" required />
          <input v-model="newIngredient.unit" type="text" placeholder="Einheit" required />
          <button type="submit" :disabled="!selectedRecipeId">Speichern</button>
        </form>
      </div>
    </div>

    <div class="card">
      <h3>Rezept in Einkaufsliste übernehmen</h3>

      <div class="form">
        <select v-model="selectedShoppingListId">
          <option disabled value="">Einkaufsliste wählen</option>
          <option v-for="list in shoppingLists" :key="list.id" :value="list.id">
            {{ list.name }}
          </option>
        </select>

        <button
          type="button"
          :disabled="!selectedRecipeId || !selectedShoppingListId"
          @click="addRecipeToShoppingList"
        >
          Zutaten in Einkaufsliste übernehmen
        </button>
      </div>
    </div>

    <div class="card">
      <h3>Rezepte</h3>

      <table v-if="recipes.length > 0">
        <thead>
          <tr>
            <th>Name</th>
            <th>Basis-Portionen</th>
            <th>Beschreibung</th>
            <th>Zutaten</th>
            <th>Aktionen</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="recipe in recipes"
            :key="recipe.id"
            :class="{ selected: selectedRecipeId === recipe.id }"
          >
            <template v-if="editRecipeId === recipe.id">
              <td>
                <input v-model="editRecipe.name" type="text" />
              </td>
              <td>
                <input v-model="editRecipe.baseServings" type="number" min="1" step="1" />
              </td>
              <td>
                <textarea v-model="editRecipe.description"></textarea>
              </td>
              <td>{{ recipe.ingredients.length }}</td>
              <td class="actions">
                <button @click="updateRecipe(recipe.id)">Speichern</button>
                <button @click="cancelEditRecipe">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td @click="selectedRecipeId = recipe.id" class="clickable">{{ recipe.name }}</td>
              <td>{{ recipe.baseServings }}</td>
              <td>{{ recipe.description || '—' }}</td>
              <td>{{ recipe.ingredients.length }}</td>
              <td class="actions">
                <button @click="selectedRecipeId = recipe.id">Öffnen</button>
                <button @click="startEditRecipe(recipe)">Bearbeiten</button>
                <button @click="deleteRecipe(recipe.id)">Löschen</button>
              </td>
            </template>
          </tr>
        </tbody>
      </table>

      <p v-else>Noch keine Rezepte vorhanden.</p>
    </div>

    <div class="card" v-if="selectedRecipe">
      <h3>Zutaten für „{{ selectedRecipe.name }}“</h3>
      <p><strong>Basis-Portionen:</strong> {{ selectedRecipe.baseServings }}</p>

      <table v-if="selectedRecipe.ingredients.length > 0">
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
              <td>
                <input v-model="editIngredient.name" type="text" />
              </td>
              <td>
                <input v-model="editIngredient.quantity" type="number" step="0.01" />
              </td>
              <td>
                <input v-model="editIngredient.unit" type="text" />
              </td>
              <td class="actions">
                <button @click="updateIngredient(ingredient.id)">Speichern</button>
                <button @click="cancelEditIngredient">Abbrechen</button>
              </td>
            </template>

            <template v-else>
              <td>{{ ingredient.name }}</td>
              <td>{{ ingredient.quantity }}</td>
              <td>{{ ingredient.unit }}</td>
              <td class="actions">
                <button @click="startEditIngredient(ingredient)">Bearbeiten</button>
                <button @click="deleteIngredient(ingredient.id)">Löschen</button>
              </td>
            </template>
          </tr>
        </tbody>
      </table>

      <p v-else>Dieses Rezept enthält noch keine Zutaten.</p>
    </div>
  </section>
</template>

<style scoped>
/* ── PAGE WRAPPER ─────────────────────────────────────── */
section {
  max-width: 1200px;
  margin: 0 auto;
  padding: 32px 24px;
}

/* ── PAGE HEADING ─────────────────────────────────────── */
h2 {
  font-size: 28px;
  font-weight: 800;
  color: var(--text);
  letter-spacing: -0.8px;
  margin-bottom: 4px;
}

h3 {
  font-size: 15px;
  font-weight: 700;
  color: var(--text);
  margin-bottom: 16px;
}

h4 {
  font-size: 13px;
  font-weight: 700;
  color: var(--text);
  margin-bottom: 10px;
}

h5 {
  font-size: 12px;
  font-weight: 700;
  color: var(--text-muted);
  margin-bottom: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

/* ── HEADLINE ROW ─────────────────────────────────────── */
.headline-row {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  align-items: flex-start;
  margin-bottom: 24px;
}

.muted {
  color: var(--text-muted);
  margin-top: 4px;
  font-size: 14px;
}

/* ── ALERTS ───────────────────────────────────────────── */
.error {
  display: flex;
  align-items: center;
  gap: 8px;
  background: rgba(239, 68, 68, 0.08);
  border: 1px solid rgba(239, 68, 68, 0.25);
  color: #ef4444;
  border-radius: var(--radius-sm);
  padding: 12px 16px;
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 16px;
}

.success {
  display: flex;
  align-items: center;
  gap: 8px;
  background: rgba(16, 185, 129, 0.08);
  border: 1px solid rgba(16, 185, 129, 0.25);
  color: #10b981;
  border-radius: var(--radius-sm);
  padding: 12px 16px;
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 16px;
}

/* ── SUMMARY / STAT GRID ──────────────────────────────── */
.summary-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 20px;
}

.big {
  font-size: 28px;
  font-weight: 800;
  color: var(--primary);
  letter-spacing: -1px;
  margin-top: 4px;
}

/* ── CARD ─────────────────────────────────────────────── */
.card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 24px;
  margin-bottom: 20px;
  box-shadow: var(--card-shadow);
  transition: background 0.3s ease, border-color 0.3s ease;
}

/* ── TABLE HEADER ─────────────────────────────────────── */
.table-header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: center;
  margin-bottom: 16px;
}

/* ── FORM ─────────────────────────────────────────────── */
.form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.compact-form {
  gap: 8px;
}

/* ── INPUTS ───────────────────────────────────────────── */
input:not([type="checkbox"]),
select,
textarea {
  width: 100%;
  padding: 10px 14px;
  border-radius: var(--radius-sm);
  border: 1.5px solid var(--border);
  background: var(--surface2);
  color: var(--text);
  font: inherit;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s, background 0.2s;
}

input:not([type="checkbox"]):focus,
select:focus,
textarea:focus {
  border-color: var(--primary);
  background: var(--surface);
}

textarea {
  min-height: 80px;
  resize: vertical;
}

input[type="checkbox"] {
  width: 16px;
  height: 16px;
  accent-color: var(--primary);
  cursor: pointer;
}

.checkbox-row {
  display: flex;
  gap: 8px;
  align-items: center;
  font-size: 14px;
  color: var(--text);
  cursor: pointer;
}

/* ── BUTTONS ──────────────────────────────────────────── */
button {
  padding: 9px 18px;
  border-radius: var(--radius-sm);
  border: none;
  font: inherit;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.15s, transform 0.1s, background 0.2s;
  background: var(--primary);
  color: white;
}

button:hover:not(:disabled) {
  opacity: 0.88;
  transform: translateY(-1px);
}

button:active:not(:disabled) {
  transform: translateY(0);
}

button:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

/* Secondary / cancel variant — applied by class */
button.btn-secondary,
button[data-variant="secondary"] {
  background: var(--surface2);
  color: var(--text);
  border: 1.5px solid var(--border);
}

/* ── TABLE ────────────────────────────────────────────── */
table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}

thead {
  position: sticky;
  top: 0;
  z-index: 1;
}

th {
  padding: 10px 12px;
  background: var(--surface2);
  color: var(--text-muted);
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  text-align: left;
  border-bottom: 2px solid var(--border);
}

td {
  padding: 11px 12px;
  border-bottom: 1px solid var(--border);
  text-align: left;
  vertical-align: middle;
  color: var(--text);
}

tbody tr:hover {
  background: var(--surface2);
}

tbody tr:last-child td {
  border-bottom: none;
}

/* ── GRID LAYOUT ──────────────────────────────────────── */
.grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
  margin-bottom: 20px;
}

/* ── ACTIONS CELL ─────────────────────────────────────── */
.actions {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.actions button {
  padding: 6px 12px;
  font-size: 12px;
}

/* "Abbrechen" style for inline cancel buttons */
.actions button:last-child:not(:first-child) {
  background: var(--surface2);
  color: var(--text);
  border: 1.5px solid var(--border);
}

/* ── DETAILS / NESTED ROWS ────────────────────────────── */
.details-row td {
  background: var(--surface2);
  padding: 16px 20px;
}

.details-box {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.details-meta {
  display: flex;
  gap: 24px;
  flex-wrap: wrap;
  font-size: 13px;
  color: var(--text);
}

.details-meta div strong {
  color: var(--text-muted);
  font-weight: 600;
}

.nested-table {
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  overflow: hidden;
  margin-top: 4px;
  margin-bottom: 12px;
}

.nested-table th {
  background: var(--surface);
}

/* ── SEARCH TERM CELL ─────────────────────────────────── */
.search-term-cell {
  max-width: 260px;
  word-break: break-word;
  font-size: 13px;
  color: var(--text-muted);
}

/* ── SELECTED ROW ─────────────────────────────────────── */
.selected td {
  background: rgba(99, 102, 241, 0.06);
}

.clickable {
  cursor: pointer;
  color: var(--primary);
  font-weight: 600;
}

.clickable:hover {
  text-decoration: underline;
}

/* ── LINKS ────────────────────────────────────────────── */
a {
  color: var(--primary);
  text-decoration: none;
  font-weight: 500;
}

a:hover {
  text-decoration: underline;
}

/* ── WEEK PLANNER ─────────────────────────────────────── */
.week-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 20px;
}

.week-header h3 { margin-bottom: 4px; }

.week-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.week-grid {
  display: grid;
  grid-template-columns: repeat(7, minmax(180px, 1fr));
  gap: 12px;
  overflow-x: auto;
  padding-bottom: 8px;
}

.day-column {
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  padding: 14px 12px;
  min-height: 240px;
  background: var(--surface2);
  transition: background 0.2s;
}

.day-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  font-size: 13px;
  color: var(--text);
}

.small-button {
  padding: 4px 9px;
  font-size: 12px;
  background: rgba(99,102,241,0.12);
  color: var(--primary);
  border: none;
  border-radius: 6px;
}
.small-button:hover { background: rgba(99,102,241,0.22); transform: none; }

.meal-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.meal-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  padding: 12px;
  transition: background 0.2s;
}

.meal-card.completed {
  opacity: 0.65;
  border-color: rgba(16,185,129,0.3);
  background: rgba(16,185,129,0.04);
}

.meal-topline {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 4px;
  font-size: 11px;
  color: var(--text-muted);
}

.meal-title {
  font-weight: 700;
  font-size: 13px;
  margin-bottom: 6px;
  color: var(--text);
}

.status {
  font-weight: 500;
  color: #10b981;
}

.meal-notes {
  font-size: 12px;
  color: var(--text-muted);
  margin-bottom: 8px;
  line-height: 1.4;
}

.empty-day {
  color: var(--text-muted);
  font-size: 13px;
  margin-top: 8px;
}

/* ── INGREDIENT MISSING/COVERED ───────────────────────── */
.missing {
  color: #ef4444;
  font-weight: 700;
}

.covered {
  color: #10b981;
  font-weight: 700;
}

/* ── SHOPPING LIST ────────────────────────────────────── */
.checked {
  text-decoration: line-through;
  opacity: 0.5;
}

.to-buy {
  font-weight: 700;
  color: var(--primary);
}

.hint {
  margin-bottom: 16px;
  color: var(--text-muted);
  font-size: 13px;
}

.totals {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 20px;
}

.total-box {
  background: var(--surface2);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  padding: 14px 20px;
  min-width: 200px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.total-box strong {
  font-size: 18px;
  font-weight: 800;
  color: var(--primary);
}

/* ── STORE SUMMARY ─────────────────────────────────────── */
.store-summary-card {
  margin-bottom: 20px;
}

.best td {
  background: rgba(16,185,129,0.05);
}

.best-badge {
  margin-left: 8px;
  font-size: 11px;
  background: rgba(16,185,129,0.15);
  color: #10b981;
  padding: 2px 8px;
  border-radius: 20px;
  font-weight: 700;
}

.complete {
  color: #10b981;
  font-weight: 700;
}

.partial {
  color: #f59e0b;
  font-weight: 700;
}

/* ── PRICE OPTIONS ─────────────────────────────────────── */
.price-options-row td {
  background: var(--surface2);
  padding: 16px 20px;
}

.price-options-wrapper {
  padding: 4px 0;
}

.no-price-options {
  color: var(--text-muted);
  font-size: 13px;
  margin: 8px 0 12px;
}

.price-option-form {
  border: 1.5px dashed var(--border);
  border-radius: var(--radius-sm);
  padding: 16px;
  margin-top: 12px;
  background: var(--surface);
}

.price-option-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(180px, 1fr));
  gap: 10px;
  margin-bottom: 12px;
}

/* ── RESPONSIVE ───────────────────────────────────────── */
@media (max-width: 1100px) {
  .price-option-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 1200px) {
  .week-grid {
    grid-template-columns: repeat(7, 200px);
  }
}

@media (max-width: 900px) {
  .summary-grid {
    grid-template-columns: 1fr 1fr;
  }

  .grid {
    grid-template-columns: 1fr;
  }

  .headline-row,
  .table-header,
  .week-header {
    flex-direction: column;
    align-items: stretch;
  }
}

@media (max-width: 540px) {
  .summary-grid {
    grid-template-columns: 1fr;
  }

  section {
    padding: 20px 16px;
  }
}
</style>
