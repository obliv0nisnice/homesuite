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
  weekSummary.value = loadedWeekSummary

  try {
    const [loadedRecipes, loadedMealPlans, loadedShoppingLists] = await Promise.all([
      apiFetch<RecipeOption[]>('/recipes'),
      apiFetch<MealPlan[]>('/mealplans'),
      apiFetch<ShoppingListOption[]>('/shoppinglists'),
      apiFetch<MealPlanWeekSummary>(`/mealplans/week-summary?weekStartDate=${currentWeekStartKey.value}`),
    ])

    recipes.value = loadedRecipes
    mealPlans.value = loadedMealPlans
    shoppingLists.value = loadedShoppingLists

    if (!newMealPlan.value.recipeId && loadedRecipes.length > 0) {
      newMealPlan.value.recipeId = loadedRecipes[0].id
    }

    if (!selectedShoppingListId.value && loadedShoppingLists.length > 0) {
      selectedShoppingListId.value = loadedShoppingLists[0].id
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
  <section>
    <h2>Meal Planner</h2>

    <p v-if="loading">Lade Daten...</p>
    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="success" class="success">{{ success }}</p>

    <div class="card">
      <h3>Neue Mahlzeit planen</h3>

      <form class="form" @submit.prevent="createMealPlan">
        <input v-model="newMealPlan.date" type="date" required />

        <select v-model="newMealPlan.mealType">
          <option value="Breakfast">Breakfast</option>
          <option value="Lunch">Lunch</option>
          <option value="Dinner">Dinner</option>
          <option value="Snack">Snack</option>
        </select>

        <input
          v-model="newMealPlan.servings"
          type="number"
          min="1"
          step="1"
          placeholder="Portionen"
          required
        />

        <select v-model="newMealPlan.recipeId" required>
          <option disabled value="">Rezept wählen</option>
          <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">
            {{ recipe.name }}
          </option>
        </select>

        <textarea v-model="newMealPlan.notes" placeholder="Notizen"></textarea>

        <button type="submit">Speichern</button>
      </form>
    </div>

    <div class="card">
      <h3>Woche in Einkaufsliste übernehmen</h3>

      <div class="form">
        <div><strong>Woche:</strong> {{ weekLabel }}</div>

        <select v-model="selectedShoppingListId">
          <option disabled value="">Einkaufsliste wählen</option>
          <option v-for="list in shoppingLists" :key="list.id" :value="list.id">
            {{ list.name }}
          </option>
        </select>

        <button
          type="button"
          :disabled="!selectedShoppingListId"
          @click="addWeekToShoppingList"
        >
          Ganze Woche in Einkaufsliste übernehmen
        </button>
      </div>
    </div>

    <div class="card" v-if="weekSummary">
  <h3>Wochenbedarf mit Inventar-Abgleich</h3>

  <table v-if="weekSummary.ingredients.length > 0">
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
        <td :class="{ missing: ingredient.missingQuantity > 0, covered: ingredient.missingQuantity <= 0 }">
          {{ ingredient.missingQuantity }}
        </td>
        <td>{{ ingredient.unit }}</td>
      </tr>
    </tbody>
  </table>

  <p v-else>Für diese Woche gibt es noch keinen Zutatenbedarf.</p>
</div>

    <div class="card">
      <div class="week-header">
        <div>
          <h3>Wochenansicht</h3>
          <p>{{ weekLabel }}</p>
        </div>

        <div class="week-actions">
          <button @click="previousWeek">← Vorherige</button>
          <button @click="currentWeek">Diese Woche</button>
          <button @click="nextWeek">Nächste →</button>
        </div>
      </div>

      <div class="week-grid">
        <div v-for="day in weekDays" :key="day.key" class="day-column">
          <div class="day-header">
            <strong>{{ day.label }}</strong>
            <button class="small-button" @click="planMealForDay(day.key)">+ Planen</button>
          </div>

          <div v-if="day.items.length > 0" class="meal-list">
            <div
              v-for="mealPlan in day.items"
              :key="mealPlan.id"
              class="meal-card"
              :class="{ completed: mealPlan.isCompleted }"
            >
              <template v-if="editingMealPlanId === mealPlan.id">
                <div class="form compact-form">
                  <input v-model="editMealPlan.date" type="date" />

                  <select v-model="editMealPlan.mealType">
                    <option value="Breakfast">Breakfast</option>
                    <option value="Lunch">Lunch</option>
                    <option value="Dinner">Dinner</option>
                    <option value="Snack">Snack</option>
                  </select>

                  <select v-model="editMealPlan.recipeId">
                    <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">
                      {{ recipe.name }}
                    </option>
                  </select>

                  <input v-model="editMealPlan.servings" type="number" min="1" step="1" />

                  <textarea v-model="editMealPlan.notes" placeholder="Notizen"></textarea>

                  <div class="actions">
                    <button @click="updateMealPlan(mealPlan.id)">Speichern</button>
                    <button @click="cancelEditMealPlan">Abbrechen</button>
                  </div>
                </div>
              </template>

              <template v-else>
                <div class="meal-topline">
                  <strong>{{ mealPlan.mealType }}</strong>
                  <span>{{ mealPlan.servings }} Portionen</span>
                </div>

                <div class="meal-title">
                  {{ mealPlan.recipeName }}
                  <span v-if="mealPlan.isCompleted" class="status">· gekocht</span>
                </div>

                <div v-if="mealPlan.notes" class="meal-notes">
                  {{ mealPlan.notes }}
                </div>

                <div class="actions">
                  <button @click="startEditMealPlan(mealPlan)" :disabled="mealPlan.isCompleted">
                    Bearbeiten
                  </button>
                  <button @click="deleteMealPlan(mealPlan.id)">Löschen</button>
                  <button
                    @click="completeMealPlan(mealPlan.id)"
                    :disabled="mealPlan.isCompleted"
                  >
                    Als gekocht markieren
                  </button>
                </div>
              </template>
            </div>
          </div>

          <p v-else class="empty-day">Keine Mahlzeiten geplant.</p>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>

.missing {
  color: #b00020;
  font-weight: 600;
}

.covered {
  color: #0a7a2f;
  font-weight: 600;
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

.compact-form {
  gap: 8px;
}

input,
select,
textarea,
button {
  padding: 10px;
  font: inherit;
}

textarea {
  min-height: 80px;
  resize: vertical;
}

.week-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 16px;
}

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
}

.day-column {
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 12px;
  min-height: 240px;
  background: #fafafa;
}

.day-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.small-button {
  padding: 6px 10px;
}

.meal-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.meal-card {
  background: white;
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 10px;
}

.meal-card.completed {
  opacity: 0.75;
  background: #f2f8f2;
}

.meal-topline {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 6px;
}

.meal-title {
  font-weight: 600;
  margin-bottom: 6px;
}

.status {
  font-weight: 400;
  color: #0a7a2f;
}

.meal-notes {
  font-size: 0.95rem;
  opacity: 0.85;
  margin-bottom: 8px;
}

.actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 8px;
}

.empty-day {
  opacity: 0.7;
  font-size: 0.95rem;
}

.error {
  color: #b00020;
  margin-bottom: 16px;
}

.success {
  color: #0a7a2f;
  margin-bottom: 16px;
}

@media (max-width: 1200px) {
  .week-grid {
    grid-template-columns: repeat(7, 220px);
  }
}

@media (max-width: 900px) {
  .week-header {
    flex-direction: column;
  }
}
</style>
