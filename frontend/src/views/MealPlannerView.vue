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
