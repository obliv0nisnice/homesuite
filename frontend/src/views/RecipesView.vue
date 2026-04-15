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
})

const editRecipeId = ref<string | null>(null)
const editRecipe = ref({
  name: '',
  description: '',
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
      body: JSON.stringify(newRecipe.value),
    })

    newRecipe.value = {
      name: '',
      description: '',
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
  }
}

function cancelEditRecipe() {
  editRecipeId.value = null
  editRecipe.value = {
    name: '',
    description: '',
  }
}

async function updateRecipe(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<Recipe>(`/recipes/${id}`, {
      method: 'PUT',
      body: JSON.stringify(editRecipe.value),
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
textarea,
select,
button {
  padding: 10px;
  font: inherit;
}

textarea {
  min-height: 100px;
  resize: vertical;
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

@media (max-width: 900px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
