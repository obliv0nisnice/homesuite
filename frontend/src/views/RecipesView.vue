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

type CatalogItem = {
  id: string
  name: string
  defaultUnit: string
}

const recipes = ref<Recipe[]>([])
const shoppingLists = ref<ShoppingListOption[]>([])
const catalogItems = ref<CatalogItem[]>([])
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

const newIngredientSuggestions = computed(() => getCatalogSuggestions(newIngredient.value.name))
const editIngredientSuggestions = computed(() => getCatalogSuggestions(editIngredient.value.name))

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
    const [recipeData, shoppingListData, catalogItemData] = await Promise.all([
      apiFetch<Recipe[]>('/recipes'),
      apiFetch<ShoppingListOption[]>('/shoppinglists'),
      apiFetch<CatalogItem[]>('/catalog'),
    ])

    recipes.value = recipeData
    shoppingLists.value = shoppingListData
    catalogItems.value = catalogItemData

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
  <BContainer class="py-4 d-flex flex-column gap-4">
    <div>
      <h1 class="h2 fw-bold mb-1">Rezepte <span class="text-primary">Kochbuch</span></h1>
      <p class="text-secondary mb-0">
        Rezepte pflegen, Zutaten strukturieren und direkt in Einkaufslisten überführen.
      </p>
    </div>

    <BAlert :model-value="!!error" variant="danger">{{ error }}</BAlert>
    <BAlert :model-value="!!success" variant="success">{{ success }}</BAlert>
    <BAlert :model-value="loading" variant="info">Lade Rezeptdaten…</BAlert>

    <BRow class="g-4">
      <BCol lg="4">
        <div class="d-flex flex-column gap-4">
          <BCard title="Neues Rezept">
            <p class="text-secondary small">Name, Beschreibung und Basis-Portionen als saubere Grundlage.</p>

            <BForm @submit.prevent="createRecipe">
              <BFormInput v-model="newRecipe.name" type="text" placeholder="Rezeptname" required class="mb-2" />
              <BFormInput
                v-model="newRecipe.baseServings"
                type="number"
                min="1"
                step="1"
                placeholder="Basis-Portionen"
                required
                class="mb-2"
              />
              <BFormTextarea v-model="newRecipe.description" placeholder="Beschreibung" rows="3" class="mb-3" />
              <BButton type="submit" variant="primary">Rezept speichern</BButton>
            </BForm>
          </BCard>

          <BCard title="Rezept in Einkaufsliste übernehmen">
            <p class="text-secondary small">Ein einzelnes Rezept gezielt auf eine bestehende Liste setzen.</p>

            <BFormSelect v-model="selectedRecipeId" class="mb-2">
              <option disabled value="">Rezept wählen</option>
              <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">{{ recipe.name }}</option>
            </BFormSelect>
            <BFormSelect v-model="selectedShoppingListId" class="mb-3">
              <option disabled value="">Einkaufsliste wählen</option>
              <option v-for="list in shoppingLists" :key="list.id" :value="list.id">{{ list.name }}</option>
            </BFormSelect>

            <BButton type="button" variant="primary" @click="addRecipeToShoppingList">
              Zur Einkaufsliste hinzufügen
            </BButton>
          </BCard>
        </div>
      </BCol>

      <BCol lg="8">
        <BCard title="Rezeptübersicht">
          <p class="text-secondary small">Rezept auswählen, bearbeiten und Zutaten direkt pflegen.</p>

          <div v-if="recipes.length > 0" class="d-flex flex-column gap-3">
            <BCard v-for="recipe in recipes" :key="recipe.id" class="bg-body-tertiary">
              <template v-if="editRecipeId === recipe.id">
                <BFormInput v-model="editRecipe.name" type="text" class="mb-2" />
                <BFormInput v-model="editRecipe.baseServings" type="number" min="1" step="1" class="mb-2" />
                <BFormTextarea v-model="editRecipe.description" placeholder="Beschreibung" rows="3" class="mb-3" />
                <div class="d-flex gap-2 flex-wrap">
                  <BButton size="sm" variant="primary" @click="updateRecipe(recipe.id)">Speichern</BButton>
                  <BButton size="sm" variant="outline-secondary" @click="cancelEditRecipe">Abbrechen</BButton>
                </div>
              </template>

              <template v-else>
                <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
                  <div>
                    <div class="fw-bold fs-5">{{ recipe.name }}</div>
                    <div class="text-secondary small" v-if="recipe.description">{{ recipe.description }}</div>
                  </div>
                  <BBadge variant="primary">{{ recipe.baseServings }} Portionen</BBadge>
                </div>

                <div class="d-flex gap-2 flex-wrap">
                  <BButton size="sm" variant="outline-secondary" @click="selectedRecipeId = recipe.id">Auswählen</BButton>
                  <BButton size="sm" variant="outline-secondary" @click="startEditRecipe(recipe)">Bearbeiten</BButton>
                  <BButton size="sm" variant="outline-danger" @click="deleteRecipe(recipe.id)">Löschen</BButton>
                </div>
              </template>
            </BCard>
          </div>

          <p v-else class="text-secondary mb-0">Noch keine Rezepte vorhanden.</p>
        </BCard>
      </BCol>
    </BRow>

    <BCard v-if="selectedRecipe" :title="`Zutaten für „${selectedRecipe.name}“`">
      <p class="text-secondary small">Pflege die Zutaten direkt am Rezept – sauber, knapp, eindeutig.</p>

      <BForm @submit.prevent="createIngredient">
        <BRow class="g-2">
          <BCol md="6">
            <BFormInput
              v-model="newIngredient.name"
              list="recipe-ingredient-suggestions"
              type="text"
              placeholder="Zutat"
              required
              @input="applyCatalogSuggestion(newIngredient)"
            />
          </BCol>
          <BCol md="3">
            <BFormInput v-model="newIngredient.quantity" type="number" min="0" step="0.01" placeholder="Menge" required />
          </BCol>
          <BCol md="3">
            <BFormInput v-model="newIngredient.unit" type="text" placeholder="Einheit" required />
          </BCol>
        </BRow>
        <BButton type="submit" variant="primary" class="mt-3">Zutat hinzufügen</BButton>
      </BForm>
      <datalist id="recipe-ingredient-suggestions">
        <option
          v-for="item in newIngredientSuggestions"
          :key="item.id"
          :value="item.name"
          :label="`${item.name} · ${item.defaultUnit}`"
        />
      </datalist>

      <BTableSimple v-if="selectedRecipe.ingredients.length > 0" hover responsive class="align-middle mt-3 mb-0">
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
                <BFormInput
                  v-model="editIngredient.name"
                  list="recipe-edit-ingredient-suggestions"
                  type="text"
                  @input="applyCatalogSuggestion(editIngredient)"
                />
              </td>
              <td><BFormInput v-model="editIngredient.quantity" type="number" min="0" step="0.01" /></td>
              <td><BFormInput v-model="editIngredient.unit" type="text" /></td>
              <td>
                <div class="d-flex gap-2 flex-wrap">
                  <BButton size="sm" variant="primary" @click="updateIngredient(ingredient.id)">Speichern</BButton>
                  <BButton size="sm" variant="outline-secondary" @click="cancelEditIngredient">Abbrechen</BButton>
                </div>
              </td>
            </template>

            <template v-else>
              <td>{{ ingredient.name }}</td>
              <td>{{ ingredient.quantity }}</td>
              <td>{{ ingredient.unit }}</td>
              <td>
                <div class="d-flex gap-2 flex-wrap">
                  <BButton size="sm" variant="outline-secondary" @click="startEditIngredient(ingredient)">Bearbeiten</BButton>
                  <BButton size="sm" variant="outline-danger" @click="deleteIngredient(ingredient.id)">Löschen</BButton>
                </div>
              </td>
            </template>
          </tr>
        </tbody>
      </BTableSimple>

      <p v-else class="text-secondary mt-3 mb-0">Dieses Rezept enthält noch keine Zutaten.</p>
      <datalist id="recipe-edit-ingredient-suggestions">
        <option
          v-for="item in editIngredientSuggestions"
          :key="item.id"
          :value="item.name"
          :label="`${item.name} · ${item.defaultUnit}`"
        />
      </datalist>
    </BCard>
  </BContainer>
</template>
