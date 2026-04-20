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
  <div class="dashboard-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Meal Planner <span class="title-accent">Woche</span></h1>
        <p class="page-subtitle">{{ weekLabel }} · Mahlzeiten, Bedarf und Übergabe an die Einkaufsliste.</p>
      </div>

      <div class="page-actions">
        <button class="btn-secondary" @click="previousWeek">← Vorherige</button>
        <button class="btn-secondary" @click="currentWeek">Diese Woche</button>
        <button class="btn-secondary" @click="nextWeek">Nächste →</button>
      </div>
    </div>

    <div v-if="error" class="alert alert-error">{{ error }}</div>
    <div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="alert">Lade Wochenplanung…</div>

    <div class="content-grid">
      <div class="stack">
        <div class="form-card">
          <div class="card-header">
            <div>
              <h2 class="card-title">Neue Mahlzeit planen</h2>
              <p class="card-copy">Schneller Einstieg für Frühstück, Mittag, Abendessen oder Snack.</p>
            </div>
          </div>

          <form @submit.prevent="createMealPlan">
            <div class="form-grid">
              <input v-model="newMealPlan.date" type="date" required />
              <select v-model="newMealPlan.mealType">
                <option value="Breakfast">Breakfast</option>
                <option value="Lunch">Lunch</option>
                <option value="Dinner">Dinner</option>
                <option value="Snack">Snack</option>
              </select>
              <input v-model="newMealPlan.servings" type="number" min="1" step="1" placeholder="Portionen" required />
              <select v-model="newMealPlan.recipeId" required>
                <option disabled value="">Rezept wählen</option>
                <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">{{ recipe.name }}</option>
              </select>
              <textarea v-model="newMealPlan.notes" class="field-span-2" placeholder="Notizen"></textarea>
            </div>
            <div class="form-actions">
              <button class="btn-add" type="submit">Speichern</button>
            </div>
          </form>
        </div>

        <div class="form-card">
          <div class="card-header">
            <div>
              <h2 class="card-title">Woche in Einkaufsliste übernehmen</h2>
              <p class="card-copy">Der Wochenbedarf wird gesammelt und direkt in eine Einkaufsliste geschrieben.</p>
            </div>
          </div>

          <div class="form-grid full">
            <div class="meta-tile">
              <span class="meta-label">Woche</span>
              <span class="meta-value" style="font-size: 16px;">{{ weekLabel }}</span>
            </div>
            <select v-model="selectedShoppingListId">
              <option disabled value="">Einkaufsliste wählen</option>
              <option v-for="list in shoppingLists" :key="list.id" :value="list.id">{{ list.name }}</option>
            </select>
          </div>

          <div class="form-actions">
            <button class="btn-add" type="button" :disabled="!selectedShoppingListId" @click="addWeekToShoppingList">
              Ganze Woche übernehmen
            </button>
          </div>
        </div>

        <div v-if="weekSummary" class="data-card">
          <div class="card-header">
            <div>
              <h2 class="card-title">Wochenbedarf mit Inventar-Abgleich</h2>
              <p class="card-copy">Was vorhanden ist und was noch fehlt, bevor die Woche startet.</p>
            </div>
          </div>

          <table v-if="weekSummary.ingredients.length > 0" class="data-table">
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
                  <span :class="['badge', ingredient.missingQuantity > 0 ? 'badge-warning' : 'badge-success']">
                    {{ ingredient.missingQuantity }}
                  </span>
                </td>
                <td>{{ ingredient.unit }}</td>
              </tr>
            </tbody>
          </table>

          <div v-else class="empty-state">Für diese Woche gibt es noch keinen Zutatenbedarf.</div>
        </div>
      </div>

      <div class="week-card">
        <div class="card-header">
          <div>
            <h2 class="card-title">Wochenansicht</h2>
            <p class="card-copy">{{ weekLabel }}</p>
          </div>
        </div>

        <div class="week-grid">
          <div v-for="day in weekDays" :key="day.key" class="day-column">
            <div class="day-header">
              <strong>{{ day.label }}</strong>
              <button class="small-button" @click="planMealForDay(day.key)">+ Planen</button>
            </div>

            <div v-if="day.items.length > 0" class="compact-list">
              <div v-for="mealPlan in day.items" :key="mealPlan.id" class="compact-item" :class="{ completed: mealPlan.isCompleted }">
                <template v-if="editingMealPlanId === mealPlan.id">
                  <div class="form-grid full">
                    <input v-model="editMealPlan.date" type="date" />
                    <select v-model="editMealPlan.mealType">
                      <option value="Breakfast">Breakfast</option>
                      <option value="Lunch">Lunch</option>
                      <option value="Dinner">Dinner</option>
                      <option value="Snack">Snack</option>
                    </select>
                    <select v-model="editMealPlan.recipeId">
                      <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">{{ recipe.name }}</option>
                    </select>
                    <input v-model="editMealPlan.servings" type="number" min="1" step="1" />
                    <textarea v-model="editMealPlan.notes" placeholder="Notizen"></textarea>
                  </div>
                  <div class="form-actions">
                    <button class="btn-save" @click="updateMealPlan(mealPlan.id)">Speichern</button>
                    <button class="btn-secondary" @click="cancelEditMealPlan">Abbrechen</button>
                  </div>
                </template>

                <template v-else>
                  <div class="card-header" style="margin-bottom: 10px;">
                    <div>
                      <div class="badge badge-primary">{{ mealPlan.mealType }}</div>
                      <div style="margin-top:6px; font-weight: 700;">{{ mealPlan.recipeName }}</div>
                    </div>
                    <span class="badge" :class="mealPlan.isCompleted ? 'badge-success' : 'badge-warning'">
                      {{ mealPlan.isCompleted ? 'gekocht' : mealPlan.servings + ' Portionen' }}
                    </span>
                  </div>

                  <div v-if="mealPlan.notes" class="card-copy" style="margin-bottom: 12px;">{{ mealPlan.notes }}</div>

                  <div class="actions">
                    <button class="btn-secondary" @click="startEditMealPlan(mealPlan)" :disabled="mealPlan.isCompleted">Bearbeiten</button>
                    <button class="btn-danger" @click="deleteMealPlan(mealPlan.id)">Löschen</button>
                    <button class="btn-add" @click="completeMealPlan(mealPlan.id)" :disabled="mealPlan.isCompleted">Als gekocht markieren</button>
                  </div>
                </template>
              </div>
            </div>

            <div v-else class="empty-state">Noch nichts geplant.</div>
          </div>
        </div>
      </div>
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

.week-grid {
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  gap: 14px;
}
.day-column {
  background: var(--surface2);
  border: 1px solid var(--border);
  border-radius: 18px;
  padding: 14px;
  min-height: 220px;
}
.day-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 14px;
}
.completed { opacity: 0.78; }
@media (max-width: 1180px) { .week-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
@media (max-width: 780px) { .week-grid { grid-template-columns: 1fr; } }
</style>
