<template>
  <div class="dashboard-page">
    <div class="page-header">
      <div>
        <h1 class="page-title">Dashboard <span class="title-accent">Planer</span></h1>
        <p class="page-subtitle">{{ monthLabel }} · Termine und Essensplanung</p>
      </div>

      <div class="header-actions">
        <button class="btn-secondary" @click="showSubscriptionModal = true">Kalender-Abos</button>
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

    <div v-if="error" class="alert alert-error">⚠ {{ error }}</div>
    <div v-if="success" class="alert alert-success">✓ {{ success }}</div>

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
              <button class="btn-add-small" @click="openEventModal">+ Termin</button>
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
                <div v-if="event.isImported" class="subscription-chip">
                  📡 {{ event.sourceName || 'Kalender-Abo' }}
                </div>
                <div v-if="event.notes" class="item-notes">{{ event.notes }}</div>
                <button v-if="!event.isImported" class="btn-delete" @click="deleteEvent(event.id)">Löschen</button>
              </div>
            </div>
          </div>

          <div class="day-section">
            <h3>Essensplanung</h3>
            <div v-if="selectedDayMeals.length === 0" class="empty-state">
              Keine Meals geplant.
            </div>
            <div v-else class="item-list">
              <div v-for="meal in selectedDayMeals" :key="meal.id" class="item-card">
                <div class="item-main">
                  <strong>{{ meal.mealType }} · {{ meal.recipeName || 'Rezept' }}</strong>
                  <span class="item-time">{{ meal.servings }} Portion(en)</span>
                </div>
                <div v-if="meal.isCompleted" class="subscription-chip">
                  ✅ Erledigt
                </div>
                <div v-if="meal.notes" class="item-notes">{{ meal.notes }}</div>
                <div class="item-actions">
                  <button
                    v-if="!meal.isCompleted"
                    class="btn-save btn-inline"
                    @click="completeMeal(meal.id)"
                  >
                    Als gekocht markieren
                  </button>
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
                <label>Von</label>
                <input v-model="eventForm.startDate" class="form-input" type="date" />
              </div>

              <div class="form-group">
                <label>Bis</label>
                <input v-model="eventForm.endDate" class="form-input" type="date" />
              </div>
            </div>

            <label class="toggle-row">
              <span>Ganztägig</span>
              <input v-model="eventForm.isAllDay" type="checkbox" />
            </label>

            <div v-if="!eventForm.isAllDay" class="form-row">
              <div class="form-group">
                <label>Start</label>
                <input v-model="eventForm.startTime" class="form-input" type="time" step="60" />
              </div>

              <div class="form-group">
                <label>Ende</label>
                <input v-model="eventForm.endTime" class="form-input" type="time" step="60" />
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

    <Teleport to="body">
      <div v-if="showSubscriptionModal" class="modal-backdrop" @click.self="showSubscriptionModal = false">
        <div class="modal-box">
          <div class="modal-header">
            <h2>Kalender-Abos</h2>
            <button class="modal-close" @click="showSubscriptionModal = false">✕</button>
          </div>

          <div class="modal-body">
            <p class="modal-hint">ICS- oder Kalender-Feed-URL hinterlegen. Die Abos werden im Backend gespeichert und im Dashboard eingeblendet.</p>

            <div class="form-row subscription-add-row">
              <div class="form-group subscription-url-group">
                <label>Kalender-URL</label>
                <input v-model="subscriptionUrl" class="form-input" type="url" placeholder="https://…" />
              </div>
              <div class="form-group subscription-action-group">
                <label>&nbsp;</label>
                <button class="btn-save" @click="addSubscription">Hinzufügen</button>
              </div>
            </div>

            <div v-if="calendarSubscriptions.length === 0" class="empty-state">Noch keine Kalender-Abos hinterlegt.</div>
            <div v-else class="item-list">
              <div v-for="subscription in calendarSubscriptions" :key="subscription.id" class="item-card">
                <div class="item-main">
                  <strong>{{ subscription.url }}</strong>
                </div>
                <button class="btn-delete" @click="removeSubscription(subscription.id)">Entfernen</button>
              </div>
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn-cancel" @click="showSubscriptionModal = false">Schließen</button>
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
  isImported?: boolean
  sourceName?: string | null
  sourceUrl?: string | null
}

type CalendarSubscriptionPreview = {
  url: string
  calendarName?: string | null
  events: CalendarEvent[]
}

type CalendarSubscription = {
  id: string
  url: string
  createdAt: string
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
const showSubscriptionModal = ref(false)
const error = ref('')
const success = ref('')
const subscriptionUrl = ref('')
const calendarSubscriptions = ref<CalendarSubscription[]>([])

const eventForm = ref({
  title: '',
  startDate: selectedDate.value,
  endDate: selectedDate.value,
  isAllDay: false,
  startTime: '09:00',
  endTime: '10:00',
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

function parseIsoDate(value: string) {
  if (!isIsoDate(value)) return null
  const parts = value.split('-').map(Number)
  if (parts.length !== 3 || parts.some(Number.isNaN)) return null

  const [year, month, day] = parts as [number, number, number]
  return new Date(year, month - 1, day)
}

function addDaysToIsoDate(value: string, days: number) {
  const date = parseIsoDate(value)
  if (!date) return null
  date.setDate(date.getDate() + days)
  return formatIsoDate(date)
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
  const formatTime = (value?: string | null) => value ? value.slice(0, 5) : ''
  const formattedStart = formatTime(start)
  const formattedEnd = formatTime(end)

  if (!formattedStart && !formattedEnd) return 'Ganztägig'
  if (formattedStart && formattedEnd) return `${formattedStart} – ${formattedEnd}`
  return formattedStart || formattedEnd || 'Ganztägig'
}

function getEventsForDay(isoDate: string) {
  return events.value.filter(x => x.date === isoDate)
}

function getMealsForDay(isoDate: string) {
  return mealPlans.value.filter(x => x.date === isoDate)
}

async function loadCalendarSubscriptions() {
  calendarSubscriptions.value = await apiFetch<CalendarSubscription[]>('/CalendarEvents/subscriptions')
}

async function addSubscription() {
  const url = subscriptionUrl.value.trim()
  if (!url) {
    error.value = 'Bitte eine Kalender-URL eingeben.'
    return
  }

  try {
    const parsed = new URL(url)
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
      throw new Error('unsupported')
    }
  } catch {
    error.value = 'Bitte eine gültige http(s)-Kalender-URL eingeben.'
    return
  }

  error.value = ''
  success.value = ''

  try {
    await apiFetch<CalendarSubscription>('/CalendarEvents/subscriptions', {
      method: 'POST',
      body: JSON.stringify({ url }),
    })
    await loadCalendarSubscriptions()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Kalender-Abo konnte nicht gespeichert werden.'
    return
  }

  subscriptionUrl.value = ''
  success.value = 'Kalender-Abo gespeichert.'
  await loadMonthData()
}

async function removeSubscription(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch(`/CalendarEvents/subscriptions/${id}`, { method: 'DELETE' })
    await loadCalendarSubscriptions()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Kalender-Abo konnte nicht entfernt werden.'
    return
  }

  success.value = 'Kalender-Abo entfernt.'
  await loadMonthData()
}

function isIsoDate(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value)
}

function normalizeTimeValue(value: string) {
  const trimmed = value.trim()
  if (!trimmed) return ''

  const match = trimmed.match(/^(\d{1,2}):(\d{2})$/)
  if (!match) return null

  const hours = Number(match[1])
  const minutes = Number(match[2])

  if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
    return null
  }

  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`
}

function toMinutes(value: string) {
  const parts = value.split(':').map(Number)
  if (parts.length !== 2 || parts.some(Number.isNaN)) return 0

  const [hours, minutes] = parts as [number, number]
  return hours * 60 + minutes
}

function addOneHour(value: string) {
  const normalized = normalizeTimeValue(value)
  if (!normalized) return ''

  const totalMinutes = (toMinutes(normalized) + 60) % (24 * 60)
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`
}

function toBackendTimeValue(value: string) {
  return value ? `${value}:00` : null
}

function resetEventForm() {
  eventForm.value = {
    title: '',
    startDate: selectedDate.value,
    endDate: selectedDate.value,
    isAllDay: false,
    startTime: '09:00',
    endTime: '10:00',
    notes: '',
  }
}

function openEventModal() {
  error.value = ''
  success.value = ''
  resetEventForm()
  showEventModal.value = true
}

async function loadMonthData() {
  const subscriptions = calendarSubscriptions.value.map((subscription) => subscription.url)
  const [eventData, mealData] = await Promise.all([
    apiFetch<CalendarEvent[]>(`/CalendarEvents?year=${currentYear.value}&month=${currentMonth.value}`),
    apiFetch<MealPlan[]>(`/MealPlans/month?year=${currentYear.value}&month=${currentMonth.value}`),
  ])

  let importedData: CalendarSubscriptionPreview[] = []
  if (subscriptions.length > 0) {
    try {
      importedData = await apiFetch<CalendarSubscriptionPreview[]>('/CalendarEvents/imported', {
        method: 'POST',
        body: JSON.stringify({
          year: currentYear.value,
          month: currentMonth.value,
          urls: subscriptions,
        }),
      })
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Kalender-Abo konnte nicht geladen werden.'
    }
  }

  events.value = [
    ...(eventData ?? []),
    ...((importedData ?? []).flatMap((subscription) => subscription.events)),
  ]
  mealPlans.value = mealData ?? []
}

async function loadRecipes() {
  recipes.value = await apiFetch<Recipe[]>('/recipes')
}

async function createEvent() {
  error.value = ''
  success.value = ''

  const title = eventForm.value.title.trim()
  const startDate = eventForm.value.startDate
  const endDate = eventForm.value.endDate
  const startTime = eventForm.value.isAllDay ? '' : normalizeTimeValue(eventForm.value.startTime)
  const endTime = eventForm.value.isAllDay ? '' : normalizeTimeValue(eventForm.value.endTime)

  if (!title) {
    error.value = 'Bitte einen Titel für den Termin angeben.'
    return
  }

  if (!isIsoDate(startDate) || !isIsoDate(endDate)) {
    error.value = 'Bitte gültige Start- und Enddaten angeben.'
    return
  }

  if (startTime === null || endTime === null) {
    error.value = 'Bitte Uhrzeiten im Format HH:mm eingeben.'
    return
  }

  if (endDate < startDate) {
    error.value = 'Das Enddatum darf nicht vor dem Startdatum liegen.'
    return
  }

  if (startTime && endTime && toMinutes(endTime) < toMinutes(startTime)) {
    error.value = 'Die Endzeit darf nicht vor der Startzeit liegen.'
    return
  }

  const dates: string[] = []
  let currentDate = startDate
  while (currentDate <= endDate) {
    dates.push(currentDate)
    const nextDate = addDaysToIsoDate(currentDate, 1)
    if (!nextDate) {
      error.value = 'Der Terminzeitraum konnte nicht verarbeitet werden.'
      return
    }
    currentDate = nextDate
  }

  try {
    await Promise.all(
      dates.map((date) =>
        apiFetch('/CalendarEvents', {
          method: 'POST',
          body: JSON.stringify({
            date,
            title,
            startTime: toBackendTimeValue(startTime || ''),
            endTime: toBackendTimeValue(endTime || ''),
            notes: eventForm.value.notes.trim() || null,
          }),
        }),
      ),
    )
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Termin konnte nicht gespeichert werden.'
    return
  }

  showEventModal.value = false
  success.value = dates.length === 1 ? 'Termin gespeichert.' : `${dates.length} Termine gespeichert.`
  resetEventForm()
  await loadMonthData()
}

async function deleteEvent(id: string) {
  error.value = ''
  success.value = ''
  try {
    await apiFetch(`/CalendarEvents/${id}`, { method: 'DELETE' })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Termin konnte nicht gelöscht werden.'
    return
  }
  success.value = 'Termin gelöscht.'
  await loadMonthData()
}

async function createMeal() {
  error.value = ''
  success.value = ''
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
  success.value = 'Meal gespeichert.'
  await loadMonthData()
}

async function deleteMeal(id: string) {
  error.value = ''
  success.value = ''
  await apiFetch(`/MealPlans/${id}`, {
    method: 'DELETE',
  })

  success.value = 'Meal gelöscht.'
  await loadMonthData()
}

async function completeMeal(id: string) {
  error.value = ''
  success.value = ''

  try {
    await apiFetch(`/MealPlans/${id}/complete`, {
      method: 'POST',
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Meal konnte nicht abgeschlossen werden.'
    return
  }

  success.value = 'Meal als erledigt markiert und Inventar aktualisiert.'
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

watch(selectedDate, () => {
  if (!showEventModal.value) return
  eventForm.value.startDate = selectedDate.value
  eventForm.value.endDate = selectedDate.value
})

watch(() => eventForm.value.startTime, (value) => {
  if (eventForm.value.isAllDay) return
  const normalized = normalizeTimeValue(value)
  if (!normalized) return

  const currentEnd = normalizeTimeValue(eventForm.value.endTime)
  if (!currentEnd || currentEnd === addOneHour(normalized) || toMinutes(currentEnd) <= toMinutes(normalized)) {
    eventForm.value.endTime = addOneHour(normalized)
  }
})

watch(() => eventForm.value.isAllDay, (isAllDay) => {
  if (isAllDay) return
  if (!normalizeTimeValue(eventForm.value.startTime)) {
    eventForm.value.startTime = '09:00'
  }
  if (!normalizeTimeValue(eventForm.value.endTime)) {
    eventForm.value.endTime = addOneHour(eventForm.value.startTime)
  }
})

onMounted(async () => {
  resetEventForm()
  await Promise.all([loadCalendarSubscriptions(), loadRecipes()])
  await loadMonthData()
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

.alert {
  padding: 12px 16px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
}

.alert-error {
  background: rgba(239,68,68,0.1);
  color: #ef4444;
  border: 1px solid rgba(239,68,68,0.2);
}

.alert-success {
  background: rgba(16,185,129,0.1);
  color: #10b981;
  border: 1px solid rgba(16,185,129,0.2);
}

.header-actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
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

.subscription-chip {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  padding: 4px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
  background: rgba(99,102,241,0.12);
  color: var(--primary);
}

.item-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  flex-wrap: wrap;
}

.btn-inline {
  align-self: flex-end;
  padding: 6px 10px;
  font-size: 12px;
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

.modal-hint {
  margin: 0;
  color: var(--text-muted);
  font-size: 13px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.toggle-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 14px;
  border-radius: 10px;
  border: 1.5px solid var(--border);
  background: var(--surface2);
  color: var(--text);
  font-size: 14px;
  font-weight: 600;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.subscription-add-row {
  align-items: end;
}

.subscription-url-group {
  grid-column: span 1;
}

.subscription-action-group {
  min-width: 140px;
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
