<template>
  <div class="dashboard-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Dashboard <span class="title-accent">Planer</span></h1>
        <p class="page-subtitle">{{ monthLabel }} · Termine und Essensplanung</p>
      </div>

      <div class="header-actions">
        <button class="btn-secondary" @click="goToPreviousMonth">←</button>
        <button class="btn-secondary" @click="goToToday">Heute</button>
        <button class="btn-secondary" @click="goToNextMonth">→</button>
      </div>
    </div>

    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-icon">📅</div>
        <div class="stat-info">
          <span class="stat-label">Termine</span>
          <span class="stat-value">{{ events.length }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>

      <div class="stat-card">
        <div class="stat-icon">🍽️</div>
        <div class="stat-info">
          <span class="stat-label">Geplante Meals</span>
          <span class="stat-value">{{ mealPlans.length }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>

      <div class="stat-card">
        <div class="stat-icon">🗓️</div>
        <div class="stat-info">
          <span class="stat-label">Ausgewählter Tag</span>
          <span class="stat-value">{{ formatSelectedDate(selectedDate) }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>

      <div class="stat-card">
        <div class="stat-icon">✅</div>
        <div class="stat-info">
          <span class="stat-label">Meals erledigt</span>
          <span class="stat-value">{{ completedMealsCount }}</span>
        </div>
        <div class="stat-bg-shape"></div>
      </div>
    </div>

    <div class="dashboard-grid">
      <div class="calendar-card">
        <div class="chart-header">
          <span class="chart-title">Monatskalender</span>
          <span class="chart-badge">{{ monthLabel }}</span>
        </div>

        <div class="calendar-weekdays">
          <span v-for="day in weekdays" :key="day">{{ day }}</span>
        </div>

        <div class="calendar-grid">
          <button
            v-for="day in calendarDays"
            :key="day.key"
            class="calendar-day"
            :class="{
              muted: !day.isCurrentMonth,
              today: day.isToday,
              selected: day.iso === selectedDate
            }"
            @click="selectedDate = day.iso"
          >
            <div class="day-number">{{ day.dayNumber }}</div>

            <div class="day-indicators">
              <span v-if="getEventsForDay(day.iso).length > 0" class="indicator indicator-event">
                {{ getEventsForDay(day.iso).length }} Termin{{ getEventsForDay(day.iso).length > 1 ? 'e' : '' }}
              </span>
              <span v-if="getMealsForDay(day.iso).length > 0" class="indicator indicator-meal">
                {{ getMealsForDay(day.iso).length }} Meal{{ getMealsForDay(day.iso).length > 1 ? 's' : '' }}
              </span>
            </div>
          </button>
        </div>
      </div>

      <div class="side-panel">
        <div class="panel-card">
          <div class="chart-header">
            <span class="chart-title">Tag · {{ formatSelectedDate(selectedDate) }}</span>
            <div class="panel-actions">
              <button class="btn-add-small" @click="showEventModal = true">+ Termin</button>
              <button class="btn-add-small" @click="showMealModal = true">+ Meal</button>
            </div>
          </div>

          <div class="day-section">
            <h3>Termine</h3>
            <div v-if="selectedDayEvents.length === 0" class="empty-state">Keine Termine.</div>
            <div v-else class="item-list">
              <div v-for="event in selectedDayEvents" :key="event.id" class="item-card">
                <div class="item-main">
                  <strong>{{ event.title }}</strong>
                  <span class="item-time">
                    {{ formatTimeRange(event.startTime, event.endTime) }}
                  </span>
                </div>
                <div v-if="event.notes" class="item-notes">{{ event.notes }}</div>
                <button class="btn-delete" @click="deleteEvent(event.id)">Löschen</button>
              </div>
            </div>
          </div>

          <div class="day-section">
            <h3>Essensplanung</h3>
            <div v-if="selectedDayMeals.length === 0" class="empty-state">Keine Meals geplant.</div>
            <div v-else class="item-list">
              <div v-else class="item-list">
  <div v-for="meal in selectedDayMeals" :key="meal.id" class="item-card">
    <div class="item-main">
      <strong>{{ meal.mealType }} · {{ meal.recipeName || 'Rezept' }}</strong>
      <span class="item-time">{{ meal.servings }} Portion(en)</span>
    </div>
    <div v-if="meal.notes" class="item-notes">{{ meal.notes }}</div>
    <button class="btn-delete" @click="deleteMeal(meal.id)">Löschen</button>
  </div>
</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <Teleport to="body">
      <div v-if="showEventModal" class="modal-backdrop" @click.self="showEventModal = false">
        <div class="modal-box">
          <div class="modal-header">
            <h2>Neuer Termin</h2>
            <button class="modal-close" @click="showEventModal = false">✕</button>
          </div>

          <div class="modal-body">
            <div class="form-group">
              <label>Titel</label>
              <input v-model="eventForm.title" class="form-input" type="text" />
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Start</label>
                <input v-model="eventForm.startTime" class="form-input" type="time" />
              </div>

              <div class="form-group">
                <label>Ende</label>
                <input v-model="eventForm.endTime" class="form-input" type="time" />
              </div>
            </div>

            <div class="form-group">
              <label>Notizen</label>
              <textarea v-model="eventForm.notes" class="form-input textarea"></textarea>
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn-cancel" @click="showEventModal = false">Abbrechen</button>
            <button class="btn-save" @click="createEvent">Speichern</button>
          </div>
        </div>
      </div>
    </Teleport>

    <Teleport to="body">
      <div v-if="showMealModal" class="modal-backdrop" @click.self="showMealModal = false">
        <div class="modal-box">
          <div class="modal-header">
            <h2>Meal planen</h2>
            <button class="modal-close" @click="showMealModal = false">✕</button>
          </div>

          <div class="modal-body">
            <div class="form-group">
              <label>Rezept</label>
              <select v-model="mealForm.recipeId" class="form-input">
                <option value="">Bitte wählen</option>
                <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">
                  {{ recipe.name }}
                </option>
              </select>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Mahlzeit</label>
                <select v-model="mealForm.mealType" class="form-input">
                  <option>Breakfast</option>
                  <option>Lunch</option>
                  <option>Dinner</option>
                  <option>Snack</option>
                </select>
              </div>

              <div class="form-group">
                <label>Portionen</label>
                <input v-model.number="mealForm.servings" class="form-input" type="number" min="1" />
              </div>
            </div>

            <div class="form-group">
              <label>Notizen</label>
              <textarea v-model="mealForm.notes" class="form-input textarea"></textarea>
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn-cancel" @click="showMealModal = false">Abbrechen</button>
            <button class="btn-save" @click="createMeal">Speichern</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { apiFetch } from '@/services/api'

type CalendarEvent = {
  id: string
  date: string
  title: string
  startTime?: string | null
  endTime?: string | null
  notes?: string | null
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

type Recipe = {
  id: string
  name: string
}

const today = new Date()
const currentYear = ref(today.getFullYear())
const currentMonth = ref(today.getMonth() + 1)
const selectedDate = ref(formatIsoDate(today))

const events = ref<CalendarEvent[]>([])
const mealPlans = ref<MealPlan[]>([])
const recipes = ref<Recipe[]>([])

const showEventModal = ref(false)
const showMealModal = ref(false)

const eventForm = ref({
  title: '',
  startTime: '',
  endTime: '',
  notes: '',
})

const mealForm = ref({
  recipeId: '',
  mealType: 'Dinner',
  servings: 1,
  notes: '',
})

const weekdays = ['Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So']

const monthLabel = computed(() =>
  new Date(currentYear.value, currentMonth.value - 1, 1).toLocaleDateString('de-AT', {
    month: 'long',
    year: 'numeric',
  }),
)

const selectedDayEvents = computed(() => getEventsForDay(selectedDate.value))
const selectedDayMeals = computed(() => getMealsForDay(selectedDate.value))
const completedMealsCount = computed(() => mealPlans.value.filter(x => x.isCompleted).length)

const calendarDays = computed(() => {
  const firstDay = new Date(currentYear.value, currentMonth.value - 1, 1)
  const lastDay = new Date(currentYear.value, currentMonth.value, 0)

  const firstWeekday = (firstDay.getDay() + 6) % 7
  const daysInMonth = lastDay.getDate()

  const prevMonthLastDay = new Date(currentYear.value, currentMonth.value - 1, 0).getDate()
  const days: Array<{
    key: string
    iso: string
    dayNumber: number
    isCurrentMonth: boolean
    isToday: boolean
  }> = []

  for (let i = firstWeekday - 1; i >= 0; i--) {
    const day = prevMonthLastDay - i
    const date = new Date(currentYear.value, currentMonth.value - 2, day)
    days.push(buildCalendarDay(date, false))
  }

  for (let day = 1; day <= daysInMonth; day++) {
    const date = new Date(currentYear.value, currentMonth.value - 1, day)
    days.push(buildCalendarDay(date, true))
  }

  while (days.length < 42) {
    const nextDay = days.length - (firstWeekday + daysInMonth) + 1
    const date = new Date(currentYear.value, currentMonth.value, nextDay)
    days.push(buildCalendarDay(date, false))
  }

  return days
})

function buildCalendarDay(date: Date, isCurrentMonth: boolean) {
  const iso = formatIsoDate(date)
  return {
    key: `${iso}-${isCurrentMonth ? 'current' : 'side'}`,
    iso,
    dayNumber: date.getDate(),
    isCurrentMonth,
    isToday: iso === formatIsoDate(new Date()),
  }
}

function formatIsoDate(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatSelectedDate(value: string) {
  return new Date(value).toLocaleDateString('de-AT', {
    weekday: 'long',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

function formatTimeRange(start?: string | null, end?: string | null) {
  if (!start && !end) return 'Ganztägig'
  if (start && end) return `${start} – ${end}`
  return start || end || 'Ganztägig'
}

function getEventsForDay(isoDate: string) {
  return events.value.filter(x => x.date === isoDate)
}

function getMealsForDay(isoDate: string) {
  return mealPlans.value.filter(x => x.date === isoDate)
}

async function loadMonthData() {
  const [eventData, mealData] = await Promise.all([
  apiFetch<CalendarEvent[]>(`/CalendarEvents?year=${currentYear.value}&month=${currentMonth.value}`),
  apiFetch<MealPlan[]>(`/MealPlans/month?year=${currentYear.value}&month=${currentMonth.value}`),
])

  events.value = eventData ?? []
  mealPlans.value = mealData ?? []
}

async function loadRecipes() {
  recipes.value = await apiFetch<Recipe[]>('/recipes')
}

async function createEvent() {
  await apiFetch('/CalendarEvents', {
    method: 'POST',
    body: JSON.stringify({
      date: selectedDate.value,
      title: eventForm.value.title,
      startTime: eventForm.value.startTime || null,
      endTime: eventForm.value.endTime || null,
      notes: eventForm.value.notes || null,
    }),
  })

  eventForm.value = {
    title: '',
    startTime: '',
    endTime: '',
    notes: '',
  }

  showEventModal.value = false
  await loadMonthData()
}

async function deleteEvent(id: string) {
  await apiFetch(`/CalendarEvents/${id}`, { method: 'DELETE' })
  await loadMonthData()
}

async function createMeal() {
  await apiFetch('/mealPlans', {
    method: 'POST',
    body: JSON.stringify({
      date: selectedDate.value,
      mealType: mealForm.value.mealType,
      servings: mealForm.value.servings,
      notes: mealForm.value.notes || null,
      recipeId: mealForm.value.recipeId,
    }),
  })

  mealForm.value = {
    recipeId: '',
    mealType: 'Dinner',
    servings: 1,
    notes: '',
  }

  showMealModal.value = false
  await loadMonthData()
}

async function deleteMeal(id: string) {
  await apiFetch(`/MealPlans/${id}`, {
    method: 'DELETE',
  })

  await loadMonthData()
}

function goToPreviousMonth() {
  if (currentMonth.value === 1) {
    currentMonth.value = 12
    currentYear.value--
  } else {
    currentMonth.value--
  }
}

function goToNextMonth() {
  if (currentMonth.value === 12) {
    currentMonth.value = 1
    currentYear.value++
  } else {
    currentMonth.value++
  }
}

function goToToday() {
  const now = new Date()
  currentYear.value = now.getFullYear()
  currentMonth.value = now.getMonth() + 1
  selectedDate.value = formatIsoDate(now)
}

watch([currentYear, currentMonth], async () => {
  await loadMonthData()
})

onMounted(async () => {
  await Promise.all([loadMonthData(), loadRecipes()])
})
</script>

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
}

.title-accent { color: var(--primary); }

.page-subtitle {
  color: var(--text-muted);
  font-size: 14px;
  margin-top: 4px;
}

.header-actions {
  display: flex;
  gap: 10px;
}

.btn-secondary,
.btn-add-small,
.btn-cancel,
.btn-save {
  padding: 10px 16px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
}

.btn-secondary {
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text);
}

.btn-add-small {
  border: none;
  background: var(--primary);
  color: white;
}

.btn-cancel {
  background: none;
  border: 1px solid var(--border);
  color: var(--text);
}

.btn-save {
  border: none;
  background: var(--primary);
  color: white;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

@media (max-width: 900px) {
  .stats-grid { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 540px) {
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

.stat-icon { font-size: 32px; z-index: 1; }
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

.dashboard-grid {
  display: grid;
  grid-template-columns: 1.35fr 0.9fr;
  gap: 20px;
}

@media (max-width: 980px) {
  .dashboard-grid { grid-template-columns: 1fr; }
}

.calendar-card,
.panel-card {
  background: var(--surface);
  border-radius: var(--radius);
  padding: 24px;
  box-shadow: var(--card-shadow);
  border: 1px solid var(--border);
}

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
  flex-wrap: wrap;
  gap: 8px;
}

.chart-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--text);
}

.chart-badge {
  font-size: 12px;
  padding: 4px 10px;
  background: rgba(99,102,241,0.1);
  color: var(--primary);
  border-radius: 20px;
  font-weight: 600;
}

.panel-actions {
  display: flex;
  gap: 8px;
}

.calendar-weekdays {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 8px;
  margin-bottom: 8px;
}

.calendar-weekdays span {
  text-align: center;
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 700;
}

.calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 8px;
}

.calendar-day {
  min-height: 110px;
  background: var(--surface2);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 10px;
  text-align: left;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: all 0.15s ease;
}

.calendar-day:hover {
  transform: translateY(-1px);
  border-color: var(--primary);
}

.calendar-day.muted {
  opacity: 0.45;
}

.calendar-day.today {
  border-color: var(--primary);
}

.calendar-day.selected {
  box-shadow: inset 0 0 0 2px var(--primary);
}

.day-number {
  font-size: 14px;
  font-weight: 700;
  color: var(--text);
}

.day-indicators {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.indicator {
  display: inline-block;
  padding: 4px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
  width: fit-content;
}

.indicator-event {
  background: rgba(99,102,241,0.12);
  color: var(--primary);
}

.indicator-meal {
  background: rgba(16,185,129,0.12);
  color: #10b981;
}

.day-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-top: 18px;
}

.day-section h3 {
  color: var(--text);
  font-size: 15px;
}

.item-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.item-card {
  background: var(--surface2);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.item-main {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.item-main strong {
  color: var(--text);
}

.item-time,
.item-notes,
.empty-state {
  color: var(--text-muted);
  font-size: 13px;
}

.btn-delete {
  align-self: flex-end;
  border: none;
  background: #ef4444;
  color: white;
  border-radius: 8px;
  padding: 6px 10px;
  cursor: pointer;
  font-size: 12px;
  font-weight: 700;
}

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 999;
  backdrop-filter: blur(4px);
}

.modal-box {
  background: var(--surface);
  border-radius: var(--radius);
  width: 100%;
  max-width: 520px;
  margin: 16px;
  box-shadow: 0 20px 60px rgba(0,0,0,0.3);
  border: 1px solid var(--border);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 1px solid var(--border);
}

.modal-header h2 {
  font-size: 18px;
  font-weight: 700;
  color: var(--text);
}

.modal-close {
  background: none;
  border: none;
  font-size: 18px;
  cursor: pointer;
  color: var(--text-muted);
}

.modal-body {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-group label {
  font-size: 13px;
  font-weight: 600;
  color: var(--text);
}

.form-input {
  padding: 10px 14px;
  border-radius: 10px;
  border: 1.5px solid var(--border);
  background: var(--surface2);
  color: var(--text);
  font-size: 14px;
  outline: none;
}

.form-input:focus {
  border-color: var(--primary);
}

.textarea {
  min-height: 100px;
  resize: vertical;
}

.modal-footer {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  padding: 16px 24px;
  border-top: 1px solid var(--border);
}
</style>
