<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '@/services/api'

type MealPlanWeekIngredient = {
  name: string
  requiredQuantity: number
  inventoryQuantity: number
  missingQuantity: number
  unit: string
}

type MealPlanWeekSummary = {
  weekStartDate: string
  weekEndDate: string
  ingredients: MealPlanWeekIngredient[]
}

type RecipeOption = {
  id: string
  name: string
  description?: string | null
  ingredients: Array<unknown>
}

type ShoppingListOption = {
  id: string
  name: string
  createdAt: string
  items?: Array<unknown>
}

type MealPlan = {
  id: string
  date: string
  mealType: string
  servings: number
  notes?: string | null
  isCompleted: boolean
  recipeId: string
  recipeName: string
}

type DayColumn = {
  key: string
  label: string
  items: MealPlan[]
}

const recipes = ref<RecipeOption[]>([])
const shoppingLists = ref<ShoppingListOption[]>([])
const mealPlans = ref<MealPlan[]>([])
const loading = ref(false)
const error = ref('')
const success = ref('')
const weekSummary = ref<MealPlanWeekSummary | null>(null)

const weekOffset = ref(0)
const selectedShoppingListId = ref('')

const newMealPlan = ref({
  date: '',
  mealType: 'Dinner',
  servings: 1,
  notes: '',
  recipeId: '',
})

const editingMealPlanId = ref<string | null>(null)
const editMealPlan = ref({
  date: '',
  mealType: 'Dinner',
  servings: 1,
  notes: '',
  recipeId: '',
})

function toDateInputValue(date: Date) {
  return date.toISOString().slice(0, 10)
}

function getStartOfWeek(baseDate: Date) {
  const date = new Date(baseDate)
  const day = date.getDay()
  const diff = day === 0 ? -6 : 1 - day
  date.setDate(date.getDate() + diff)
  date.setHours(0, 0, 0, 0)
  return date
}

function addDays(date: Date, days: number) {
  const copy = new Date(date)
  copy.setDate(copy.getDate() + days)
  return copy
}

const currentWeekStart = computed(() => {
  const today = new Date()
  const shifted = addDays(today, weekOffset.value * 7)
  return getStartOfWeek(shifted)
})

const currentWeekEnd = computed(() => addDays(currentWeekStart.value, 6))

const currentWeekStartKey = computed(() => toDateInputValue(currentWeekStart.value))

const weekLabel = computed(() => {
  const start = currentWeekStart.value.toLocaleDateString()
  const end = currentWeekEnd.value.toLocaleDateString()
  return `${start} - ${end}`
})

const weekDays = computed<DayColumn[]>(() => {
  const days: DayColumn[] = []

  for (let i = 0; i < 7; i++) {
    const date = addDays(currentWeekStart.value, i)
    const key = toDateInputValue(date)

    days.push({
      key,
      label: date.toLocaleDateString(undefined, {
        weekday: 'short',
        day: '2-digit',
        month: '2-digit',
      }),
      items: mealPlans.value
        .filter((x) => x.date === key)
        .sort((a, b) => a.mealType.localeCompare(b.mealType)),
    })
  }

  return days
})

async function loadData() {
  loading.value = true
  error.value = ''
  success.value = ''

  try {
    const [loadedRecipes, loadedMealPlans, loadedShoppingLists, loadedWeekSummary] = await Promise.all([
      apiFetch<RecipeOption[]>('/recipes'),
      apiFetch<MealPlan[]>('/mealplans'),
      apiFetch<ShoppingListOption[]>('/shoppinglists'),
      apiFetch<MealPlanWeekSummary>(`/mealplans/week-summary?weekStartDate=${currentWeekStartKey.value}`),
    ])

    recipes.value = loadedRecipes
    mealPlans.value = loadedMealPlans
    shoppingLists.value = loadedShoppingLists

    if (!newMealPlan.value.recipeId && loadedRecipes.length > 0) {
      newMealPlan.value.recipeId = loadedRecipes[0]?.id ?? ''
    }

    if (!selectedShoppingListId.value && loadedShoppingLists.length > 0) {
      selectedShoppingListId.value = loadedShoppingLists[0]?.id ?? ''
    }

    if (!newMealPlan.value.date) {
      newMealPlan.value.date = toDateInputValue(new Date())
    }
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Meal Plans konnten nicht geladen werden.'
  } finally {
    loading.value = false
  }
}

function planMealForDay(date: string, mealType = 'Dinner') {
  newMealPlan.value.date = date
  newMealPlan.value.mealType = mealType
}

async function createMealPlan() {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<MealPlan>('/mealplans', {
      method: 'POST',
      body: JSON.stringify({
        ...newMealPlan.value,
        servings: Number(newMealPlan.value.servings),
      }),
    })

    newMealPlan.value = {
      date: newMealPlan.value.date,
      mealType: 'Dinner',
      servings: 1,
      notes: '',
      recipeId: recipes.value[0]?.id ?? '',
    }

    success.value = 'Meal Plan wurde erstellt.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Meal Plan konnte nicht erstellt werden.'
  }
}

function startEditMealPlan(mealPlan: MealPlan) {
  editingMealPlanId.value = mealPlan.id
  editMealPlan.value = {
    date: mealPlan.date,
    mealType: mealPlan.mealType,
    servings: mealPlan.servings,
    notes: mealPlan.notes ?? '',
    recipeId: mealPlan.recipeId,
  }
}

function cancelEditMealPlan() {
  editingMealPlanId.value = null
  editMealPlan.value = {
    date: '',
    mealType: 'Dinner',
    servings: 1,
    notes: '',
    recipeId: '',
  }
}

async function updateMealPlan(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<MealPlan>(`/mealplans/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        ...editMealPlan.value,
        servings: Number(editMealPlan.value.servings),
      }),
    })

    cancelEditMealPlan()
    success.value = 'Meal Plan wurde aktualisiert.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Meal Plan konnte nicht aktualisiert werden.'
  }
}

async function deleteMealPlan(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>(`/mealplans/${id}`, {
      method: 'DELETE',
    })

    if (editingMealPlanId.value === id) {
      cancelEditMealPlan()
    }

    success.value = 'Meal Plan wurde gelöscht.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value = err instanceof Error ? err.message : 'Meal Plan konnte nicht gelöscht werden.'
  }
}

async function completeMealPlan(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch<void>(`/mealplans/${id}/complete`, {
      method: 'POST',
    })

    success.value = 'Mahlzeit wurde als gekocht markiert und vom Inventar abgezogen.'
    await loadData()
  } catch (err) {
    console.error(err)
    error.value =
      err instanceof Error
        ? err.message
        : 'Meal Plan konnte nicht abgeschlossen werden.'
  }
}

async function addWeekToShoppingList() {
  if (!selectedShoppingListId.value) {
    error.value = 'Bitte zuerst eine Einkaufsliste auswählen.'
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch(
      `/shoppinglists/${selectedShoppingListId.value}/add-mealplan-week?weekStartDate=${currentWeekStartKey.value}`,
      {
        method: 'POST',
      },
    )

    success.value = 'Die aktuelle Woche wurde zur Einkaufsliste hinzugefügt.'
  } catch (err) {
    console.error(err)
    error.value =
      err instanceof Error
        ? err.message
        : 'Die Woche konnte nicht in die Einkaufsliste übernommen werden.'
  }
}

async function previousWeek() {
  weekOffset.value -= 1
  await loadData()
}

async function nextWeek() {
  weekOffset.value += 1
  await loadData()
}

async function currentWeek() {
  weekOffset.value = 0
  await loadData()
}

onMounted(() => {
  newMealPlan.value.date = toDateInputValue(new Date())
  loadData()
})
</script>


<template>
  <BContainer class="py-4 d-flex flex-column gap-4">
    <div class="d-flex justify-content-between align-items-end flex-wrap gap-3">
      <div>
        <h1 class="h2 fw-bold mb-1">Meal Planner <span class="text-primary">Woche</span></h1>
        <p class="text-secondary mb-0">{{ weekLabel }} · Mahlzeiten, Bedarf und Übergabe an die Einkaufsliste.</p>
      </div>

      <div class="d-flex gap-2 flex-wrap">
        <BButton variant="outline-secondary" @click="previousWeek">← Vorherige</BButton>
        <BButton variant="outline-secondary" @click="currentWeek">Diese Woche</BButton>
        <BButton variant="outline-secondary" @click="nextWeek">Nächste →</BButton>
      </div>
    </div>

    <BAlert :model-value="!!error" variant="danger">{{ error }}</BAlert>
    <BAlert :model-value="!!success" variant="success">{{ success }}</BAlert>
    <BAlert :model-value="loading" variant="info">Lade Wochenplanung…</BAlert>

    <BRow class="g-4">
      <BCol lg="4">
        <div class="d-flex flex-column gap-4">
          <BCard title="Neue Mahlzeit planen">
            <p class="text-secondary small">Schneller Einstieg für Frühstück, Mittag, Abendessen oder Snack.</p>

            <BForm @submit.prevent="createMealPlan">
              <BFormInput v-model="newMealPlan.date" type="date" required class="mb-2" />
              <BFormSelect v-model="newMealPlan.mealType" class="mb-2">
                <option value="Breakfast">Breakfast</option>
                <option value="Lunch">Lunch</option>
                <option value="Dinner">Dinner</option>
                <option value="Snack">Snack</option>
              </BFormSelect>
              <BFormInput v-model="newMealPlan.servings" type="number" min="1" step="1" placeholder="Portionen" required class="mb-2" />
              <BFormSelect v-model="newMealPlan.recipeId" required class="mb-2">
                <option disabled value="">Rezept wählen</option>
                <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">{{ recipe.name }}</option>
              </BFormSelect>
              <BFormTextarea v-model="newMealPlan.notes" placeholder="Notizen" rows="3" class="mb-3" />
              <BButton type="submit" variant="primary">Speichern</BButton>
            </BForm>
          </BCard>

          <BCard title="Woche in Einkaufsliste übernehmen">
            <p class="text-secondary small">Der Wochenbedarf wird gesammelt und direkt in eine Einkaufsliste geschrieben.</p>

            <div class="border rounded p-2 mb-2">
              <div class="text-secondary text-uppercase small">Woche</div>
              <div class="fw-bold">{{ weekLabel }}</div>
            </div>
            <BFormSelect v-model="selectedShoppingListId" class="mb-3">
              <option disabled value="">Einkaufsliste wählen</option>
              <option v-for="list in shoppingLists" :key="list.id" :value="list.id">{{ list.name }}</option>
            </BFormSelect>

            <BButton type="button" variant="primary" :disabled="!selectedShoppingListId" @click="addWeekToShoppingList">
              Ganze Woche übernehmen
            </BButton>
          </BCard>

          <BCard v-if="weekSummary" title="Wochenbedarf mit Inventar-Abgleich">
            <p class="text-secondary small">Was vorhanden ist und was noch fehlt, bevor die Woche startet.</p>

            <BTableSimple v-if="weekSummary.ingredients.length > 0" small responsive class="align-middle mb-0">
              <thead>
                <tr>
                  <th>Zutat</th>
                  <th>Benötigt</th>
                  <th>Im Inventar</th>
                  <th>Fehlt</th>
                  <th>Einheit</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="ingredient in weekSummary.ingredients" :key="`${ingredient.name}-${ingredient.unit}`">
                  <td>{{ ingredient.name }}</td>
                  <td>{{ ingredient.requiredQuantity }}</td>
                  <td>{{ ingredient.inventoryQuantity }}</td>
                  <td>
                    <BBadge :variant="ingredient.missingQuantity > 0 ? 'warning' : 'success'">
                      {{ ingredient.missingQuantity }}
                    </BBadge>
                  </td>
                  <td>{{ ingredient.unit }}</td>
                </tr>
              </tbody>
            </BTableSimple>

            <p v-else class="text-secondary mb-0">Für diese Woche gibt es noch keinen Zutatenbedarf.</p>
          </BCard>
        </div>
      </BCol>

      <BCol lg="8">
        <BCard :title="`Wochenansicht`" :sub-title="weekLabel">
          <div class="week-grid mt-3">
            <BCard v-for="day in weekDays" :key="day.key" class="bg-body-tertiary" body-class="p-2">
              <div class="d-flex align-items-center justify-content-between gap-2 mb-2">
                <strong class="small">{{ day.label }}</strong>
                <BButton size="sm" variant="outline-secondary" @click="planMealForDay(day.key)">+ Planen</BButton>
              </div>

              <div v-if="day.items.length > 0" class="d-flex flex-column gap-2">
                <BCard
                  v-for="mealPlan in day.items"
                  :key="mealPlan.id"
                  body-class="p-2"
                  :class="{ 'opacity-75': mealPlan.isCompleted }"
                >
                  <template v-if="editingMealPlanId === mealPlan.id">
                    <BFormInput v-model="editMealPlan.date" type="date" size="sm" class="mb-1" />
                    <BFormSelect v-model="editMealPlan.mealType" size="sm" class="mb-1">
                      <option value="Breakfast">Breakfast</option>
                      <option value="Lunch">Lunch</option>
                      <option value="Dinner">Dinner</option>
                      <option value="Snack">Snack</option>
                    </BFormSelect>
                    <BFormSelect v-model="editMealPlan.recipeId" size="sm" class="mb-1">
                      <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">{{ recipe.name }}</option>
                    </BFormSelect>
                    <BFormInput v-model="editMealPlan.servings" type="number" min="1" step="1" size="sm" class="mb-1" />
                    <BFormTextarea v-model="editMealPlan.notes" placeholder="Notizen" rows="2" class="mb-2" />
                    <div class="d-flex gap-1 flex-wrap">
                      <BButton size="sm" variant="primary" @click="updateMealPlan(mealPlan.id)">Speichern</BButton>
                      <BButton size="sm" variant="outline-secondary" @click="cancelEditMealPlan">Abbrechen</BButton>
                    </div>
                  </template>

                  <template v-else>
                    <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
                      <div>
                        <BBadge variant="primary">{{ mealPlan.mealType }}</BBadge>
                        <div class="fw-semibold mt-1">{{ mealPlan.recipeName }}</div>
                      </div>
                      <BBadge :variant="mealPlan.isCompleted ? 'success' : 'warning'">
                        {{ mealPlan.isCompleted ? 'gekocht' : mealPlan.servings + ' Portionen' }}
                      </BBadge>
                    </div>

                    <div v-if="mealPlan.notes" class="text-secondary small mb-2">{{ mealPlan.notes }}</div>

                    <div class="d-flex gap-1 flex-wrap">
                      <BButton size="sm" variant="outline-secondary" :disabled="mealPlan.isCompleted" @click="startEditMealPlan(mealPlan)">Bearbeiten</BButton>
                      <BButton size="sm" variant="outline-danger" @click="deleteMealPlan(mealPlan.id)">Löschen</BButton>
                      <BButton size="sm" variant="primary" :disabled="mealPlan.isCompleted" @click="completeMealPlan(mealPlan.id)">Als gekocht markieren</BButton>
                    </div>
                  </template>
                </BCard>
              </div>

              <p v-else class="text-secondary small mb-0">Noch nichts geplant.</p>
            </BCard>
          </div>
        </BCard>
      </BCol>
    </BRow>
  </BContainer>
</template>

<style scoped>
/* Bootstrap hat keine 7-Spalten-Utility – minimales Grid für die Wochenansicht. */
.week-grid {
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  gap: 0.75rem;
}
@media (max-width: 1180px) { .week-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
@media (max-width: 780px) { .week-grid { grid-template-columns: 1fr; } }
</style>
