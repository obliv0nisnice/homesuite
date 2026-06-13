<template>
  <BContainer class="py-4 d-flex flex-column gap-4">
    <div class="d-flex justify-content-between align-items-end flex-wrap gap-3">
      <div>
        <h1 class="h2 fw-bold mb-1">Dashboard <span class="text-primary">Planer</span></h1>
        <p class="text-secondary mb-0">{{ monthLabel }} · Termine und Essensplanung</p>
      </div>

      <div class="d-flex gap-2 flex-wrap align-items-center">
        <BBadge :variant="syncStatusVariant">{{ syncStatusLabel }}</BBadge>
        <BButton variant="outline-secondary" @click="showSubscriptionModal = true">Kalender-Abos</BButton>
        <BButton variant="outline-secondary" @click="goToPreviousMonth">←</BButton>
        <BButton variant="outline-secondary" @click="goToToday">Heute</BButton>
        <BButton variant="outline-secondary" @click="goToNextMonth">→</BButton>
      </div>
    </div>

    <BRow class="g-3">
      <BCol md="3" sm="6">
        <BCard>
          <div class="text-secondary text-uppercase small">📅 Termine</div>
          <div class="fs-4 fw-bold">{{ events.length }}</div>
        </BCard>
      </BCol>
      <BCol md="3" sm="6">
        <BCard>
          <div class="text-secondary text-uppercase small">🍽️ Geplante Meals</div>
          <div class="fs-4 fw-bold">{{ mealPlans.length }}</div>
        </BCard>
      </BCol>
      <BCol md="3" sm="6">
        <BCard>
          <div class="text-secondary text-uppercase small">🗓️ Ausgewählter Tag</div>
          <div class="fs-5 fw-bold">{{ formatSelectedDate(selectedDate) }}</div>
        </BCard>
      </BCol>
      <BCol md="3" sm="6">
        <BCard>
          <div class="text-secondary text-uppercase small">✅ Meals erledigt</div>
          <div class="fs-4 fw-bold">{{ completedMealsCount }}</div>
        </BCard>
      </BCol>
    </BRow>

    <BAlert :model-value="!!error" variant="danger">⚠ {{ error }}</BAlert>
    <BAlert :model-value="!!success" variant="success">✓ {{ success }}</BAlert>
    <BAlert :model-value="pendingMutationCount > 0" variant="info">{{ pendingMutationLabel }}</BAlert>
    <BAlert :model-value="!!offlineSnapshotAt" variant="info">Offline-Snapshot von {{ offlineSnapshotLabel }}</BAlert>

    <BRow class="g-4">
      <BCol lg="7">
        <BCard>
          <div class="d-flex justify-content-between align-items-center mb-3">
            <span class="fw-bold">Monatskalender</span>
            <BBadge variant="primary">{{ monthLabel }}</BBadge>
          </div>

          <div class="calendar-weekdays text-secondary fw-bold small mb-2">
            <span v-for="day in weekdays" :key="day">{{ day }}</span>
          </div>

          <div class="calendar-grid">
            <button
              v-for="day in calendarDays"
              :key="day.key"
              type="button"
              class="calendar-day border rounded bg-body-tertiary text-start p-2"
              :class="{
                'opacity-50': !day.isCurrentMonth,
                'border-primary': day.isToday,
                selected: day.iso === selectedDate,
              }"
              @click="selectedDate = day.iso"
            >
              <div class="fw-bold small">{{ day.dayNumber }}</div>
              <div class="d-flex flex-column gap-1 align-items-start">
                <BBadge v-if="getEventsForDay(day.iso).length > 0" variant="primary">
                  {{ getEventsForDay(day.iso).length }} Termin{{ getEventsForDay(day.iso).length > 1 ? 'e' : '' }}
                </BBadge>
                <BBadge v-if="getMealsForDay(day.iso).length > 0" variant="success">
                  {{ getMealsForDay(day.iso).length }} Meal{{ getMealsForDay(day.iso).length > 1 ? 's' : '' }}
                </BBadge>
              </div>
            </button>
          </div>
        </BCard>
      </BCol>

      <BCol lg="5">
        <BCard>
          <div class="d-flex justify-content-between align-items-center mb-3 flex-wrap gap-2">
            <span class="fw-bold">Tag · {{ formatSelectedDate(selectedDate) }}</span>
            <div class="d-flex gap-2">
              <BButton size="sm" variant="primary" @click="openEventModal">+ Termin</BButton>
              <BButton size="sm" variant="primary" @click="showMealModal = true">+ Meal</BButton>
            </div>
          </div>

          <div class="mb-4">
            <h3 class="h6">Termine</h3>
            <p v-if="selectedDayEvents.length === 0" class="text-secondary small mb-0">Keine Termine.</p>
            <div v-else class="d-flex flex-column gap-2">
              <div v-for="event in selectedDayEvents" :key="event.id" class="border rounded p-2 bg-body-tertiary">
                <div class="d-flex justify-content-between gap-2 flex-wrap">
                  <strong>{{ event.title }}</strong>
                  <span class="text-secondary small">{{ formatTimeRange(event.startTime, event.endTime) }}</span>
                </div>
                <BBadge v-if="event.isImported" variant="primary" class="mt-1">📡 {{ event.sourceName || 'Kalender-Abo' }}</BBadge>
                <div v-if="event.notes" class="text-secondary small mt-1">{{ event.notes }}</div>
                <div v-if="!event.isImported" class="text-end mt-1">
                  <BButton size="sm" variant="outline-danger" @click="deleteEvent(event.id)">Löschen</BButton>
                </div>
              </div>
            </div>
          </div>

          <div>
            <h3 class="h6">Essensplanung</h3>
            <p v-if="selectedDayMeals.length === 0" class="text-secondary small mb-0">Keine Meals geplant.</p>
            <div v-else class="d-flex flex-column gap-2">
              <div v-for="meal in selectedDayMeals" :key="meal.id" class="border rounded p-2 bg-body-tertiary">
                <div class="d-flex justify-content-between gap-2 flex-wrap">
                  <strong>{{ meal.mealType }} · {{ meal.recipeName || 'Rezept' }}</strong>
                  <span class="text-secondary small">{{ meal.servings }} Portion(en)</span>
                </div>
                <BBadge v-if="meal.isCompleted" variant="success" class="mt-1">✅ Erledigt</BBadge>
                <div v-if="meal.notes" class="text-secondary small mt-1">{{ meal.notes }}</div>
                <div class="d-flex justify-content-end gap-2 flex-wrap mt-1">
                  <BButton v-if="!meal.isCompleted" size="sm" variant="primary" @click="completeMeal(meal.id)">
                    Als gekocht markieren
                  </BButton>
                  <BButton size="sm" variant="outline-danger" @click="deleteMeal(meal.id)">Löschen</BButton>
                </div>
              </div>
            </div>
          </div>
        </BCard>
      </BCol>
    </BRow>

    <!-- Event Modal -->
    <BModal v-model="showEventModal" title="Neuer Termin" ok-title="Speichern" cancel-title="Abbrechen" @ok="createEvent">
      <div class="d-flex flex-column gap-3">
        <div>
          <label class="form-label small fw-semibold">Titel</label>
          <BFormInput v-model="eventForm.title" type="text" />
        </div>
        <BRow class="g-2">
          <BCol>
            <label class="form-label small fw-semibold">Von</label>
            <BFormInput v-model="eventForm.startDate" type="date" />
          </BCol>
          <BCol>
            <label class="form-label small fw-semibold">Bis</label>
            <BFormInput v-model="eventForm.endDate" type="date" />
          </BCol>
        </BRow>
        <BFormCheckbox v-model="eventForm.isAllDay" switch>Ganztägig</BFormCheckbox>
        <BRow v-if="!eventForm.isAllDay" class="g-2">
          <BCol>
            <label class="form-label small fw-semibold">Start</label>
            <BFormInput v-model="eventForm.startTime" type="time" step="60" />
          </BCol>
          <BCol>
            <label class="form-label small fw-semibold">Ende</label>
            <BFormInput v-model="eventForm.endTime" type="time" step="60" />
          </BCol>
        </BRow>
        <div>
          <label class="form-label small fw-semibold">Notizen</label>
          <BFormTextarea v-model="eventForm.notes" rows="3" />
        </div>
      </div>
    </BModal>

    <!-- Meal Modal -->
    <BModal v-model="showMealModal" title="Meal planen" ok-title="Speichern" cancel-title="Abbrechen" @ok="createMeal">
      <div class="d-flex flex-column gap-3">
        <div>
          <label class="form-label small fw-semibold">Rezept</label>
          <BFormSelect v-model="mealForm.recipeId">
            <option value="">Bitte wählen</option>
            <option v-for="recipe in recipes" :key="recipe.id" :value="recipe.id">{{ recipe.name }}</option>
          </BFormSelect>
        </div>
        <BRow class="g-2">
          <BCol>
            <label class="form-label small fw-semibold">Mahlzeit</label>
            <BFormSelect v-model="mealForm.mealType">
              <option>Breakfast</option>
              <option>Lunch</option>
              <option>Dinner</option>
              <option>Snack</option>
            </BFormSelect>
          </BCol>
          <BCol>
            <label class="form-label small fw-semibold">Portionen</label>
            <BFormInput v-model.number="mealForm.servings" type="number" min="1" />
          </BCol>
        </BRow>
        <div>
          <label class="form-label small fw-semibold">Notizen</label>
          <BFormTextarea v-model="mealForm.notes" rows="3" />
        </div>
      </div>
    </BModal>

    <!-- Subscription Modal -->
    <BModal v-model="showSubscriptionModal" title="Kalender-Abos" ok-only ok-title="Schließen">
      <p class="text-secondary small">ICS- oder Kalender-Feed-URL hinterlegen. Die Abos werden im Backend gespeichert und im Dashboard eingeblendet.</p>

      <div class="d-flex gap-2 align-items-end mb-3 flex-wrap">
        <div class="flex-grow-1">
          <label class="form-label small fw-semibold">Kalender-URL</label>
          <BFormInput v-model="subscriptionUrl" type="url" placeholder="https://…" />
        </div>
        <BButton variant="primary" @click="addSubscription">Hinzufügen</BButton>
      </div>

      <p v-if="calendarSubscriptions.length === 0" class="text-secondary small mb-0">Noch keine Kalender-Abos hinterlegt.</p>
      <div v-else class="d-flex flex-column gap-2">
        <div v-for="subscription in calendarSubscriptions" :key="subscription.id" class="border rounded p-2 bg-body-tertiary d-flex justify-content-between align-items-center gap-2">
          <strong class="text-truncate">{{ subscription.url }}</strong>
          <BButton size="sm" variant="outline-danger" @click="removeSubscription(subscription.id)">Entfernen</BButton>
        </div>
      </div>
    </BModal>
  </BContainer>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { apiFetch } from '@/services/api'
import { readOfflineCache, saveOfflineCache } from '@/services/offlineCache'
import {
  enqueueOfflineMutation,
  makeTempId,
  readOfflineMutations,
  writeOfflineMutations,
} from '@/services/offlineMutations'

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

type CalendarEventPayload = {
  date: string
  title: string
  startTime?: string | null
  endTime?: string | null
  notes?: string | null
}

type MealPayload = {
  date: string
  mealType: string
  servings: number
  notes?: string | null
  recipeId: string
}

type DashboardMutation =
  | {
      type: 'createEvent'
      eventId: string
      payload: CalendarEventPayload
    }
  | {
      type: 'deleteEvent'
      eventId: string
    }
  | {
      type: 'createMeal'
      mealId: string
      payload: MealPayload
    }
  | {
      type: 'deleteMeal'
      mealId: string
    }
  | {
      type: 'completeMeal'
      mealId: string
    }
  | {
      type: 'createSubscription'
      subscriptionId: string
      url: string
    }
  | {
      type: 'deleteSubscription'
      subscriptionId: string
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
const offlineSnapshotAt = ref<string | null>(null)
const pendingMutationCount = ref(0)
const isOnline = ref(typeof navigator === 'undefined' ? true : navigator.onLine)
const isSyncing = ref(false)

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
const pendingMutationLabel = computed(() =>
  pendingMutationCount.value === 1
    ? '1 lokale Offline-Aenderung wartet auf Sync.'
    : `${pendingMutationCount.value} lokale Offline-Aenderungen warten auf Sync.`,
)
const syncStatusLabel = computed(() => {
  if (isSyncing.value) {
    return 'Synchronisiert'
  }

  if (!isOnline.value) {
    return pendingMutationCount.value > 0 ? 'Offline · wartet' : 'Offline'
  }

  if (pendingMutationCount.value > 0) {
    return 'Sync ausstehend'
  }

  return 'Online'
})
const syncStatusVariant = computed(() => {
  if (isSyncing.value) {
    return 'info'
  }

  if (!isOnline.value) {
    return pendingMutationCount.value > 0 ? 'warning' : 'secondary'
  }

  if (pendingMutationCount.value > 0) {
    return 'warning'
  }

  return 'success'
})
const offlineSnapshotLabel = computed(() => {
  if (!offlineSnapshotAt.value) {
    return ''
  }

  return new Intl.DateTimeFormat('de-AT', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(offlineSnapshotAt.value))
})

function isOfflineError(err: unknown) {
  if (typeof navigator !== 'undefined' && !navigator.onLine) {
    return true
  }

  if (err instanceof TypeError) {
    return true
  }

  return err instanceof Error && /fetch|network|offline|load/i.test(err.message)
}

function isTempId(id: string) {
  return id.startsWith('offline-')
}

function currentMonthCacheKey() {
  return `dashboard:month:${currentYear.value}-${String(currentMonth.value).padStart(2, '0')}`
}

function persistCurrentMonthSnapshot() {
  saveOfflineCache(currentMonthCacheKey(), {
    events: events.value,
    mealPlans: mealPlans.value,
  })
}

function sortEvents() {
  events.value.sort((left, right) => {
    if (left.date !== right.date) {
      return left.date.localeCompare(right.date)
    }

    return (left.startTime ?? '').localeCompare(right.startTime ?? '')
  })
}

function sortMeals() {
  mealPlans.value.sort((left, right) => {
    if (left.date !== right.date) {
      return left.date.localeCompare(right.date)
    }

    return left.mealType.localeCompare(right.mealType)
  })
}

function refreshDashboardSnapshot() {
  sortEvents()
  sortMeals()
  persistCurrentMonthSnapshot()
}

function readPendingMutations() {
  return readOfflineMutations<DashboardMutation>('dashboard')
}

function writePendingMutations(entries: ReturnType<typeof readPendingMutations>) {
  writeOfflineMutations('dashboard', entries)
  pendingMutationCount.value = entries.length
}

function refreshPendingMutationCount() {
  pendingMutationCount.value = readPendingMutations().length
}

function persistCalendarSubscriptions() {
  saveOfflineCache('dashboard:calendar-subscriptions', calendarSubscriptions.value)
}

function getRecipeName(recipeId: string) {
  return recipes.value.find((recipe) => recipe.id === recipeId)?.name ?? 'Rezept'
}

function removeQueuedCreateEvent(eventId: string) {
  const entries = readPendingMutations().filter(
    (entry) => !(entry.mutation.type === 'createEvent' && entry.mutation.eventId === eventId),
  )
  writePendingMutations(entries)
}

function removeQueuedCreateMeal(mealId: string) {
  const entries = readPendingMutations().filter(
    (entry) =>
      !(
        (entry.mutation.type === 'createMeal' && entry.mutation.mealId === mealId) ||
        (entry.mutation.type === 'completeMeal' && entry.mutation.mealId === mealId)
      ),
  )
  writePendingMutations(entries)
}

function removeQueuedCreateSubscription(subscriptionId: string) {
  const entries = readPendingMutations().filter(
    (entry) =>
      !(
        (entry.mutation.type === 'createSubscription' &&
          entry.mutation.subscriptionId === subscriptionId) ||
        (entry.mutation.type === 'deleteSubscription' &&
          entry.mutation.subscriptionId === subscriptionId)
      ),
  )
  writePendingMutations(entries)
}

async function syncPendingDashboardMutations() {
  const entries = readPendingMutations()

  if (entries.length === 0) {
    return
  }

  isSyncing.value = true
  const remaining = [...entries]
  const mealIdMap = new Map<string, string>()
  const subscriptionIdMap = new Map<string, string>()
  let syncedAny = false

  while (remaining.length > 0) {
    const current = remaining[0]
    if (!current) {
      break
    }

    try {
      switch (current.mutation.type) {
        case 'createEvent':
          await apiFetch<CalendarEvent>('/CalendarEvents', {
            method: 'POST',
            body: JSON.stringify(current.mutation.payload),
          })
          break
        case 'deleteEvent':
          await apiFetch(`/CalendarEvents/${current.mutation.eventId}`, {
            method: 'DELETE',
          })
          break
        case 'createMeal': {
          const createdMeal = await apiFetch<MealPlan>('/mealPlans', {
            method: 'POST',
            body: JSON.stringify(current.mutation.payload),
          })
          mealIdMap.set(current.mutation.mealId, createdMeal.id)
          break
        }
        case 'deleteMeal': {
          const resolvedMealId = mealIdMap.get(current.mutation.mealId) ?? current.mutation.mealId
          await apiFetch(`/MealPlans/${resolvedMealId}`, {
            method: 'DELETE',
          })
          break
        }
        case 'completeMeal': {
          const resolvedMealId = mealIdMap.get(current.mutation.mealId) ?? current.mutation.mealId
          await apiFetch(`/MealPlans/${resolvedMealId}/complete`, {
            method: 'POST',
          })
          break
        }
        case 'createSubscription': {
          const createdSubscription = await apiFetch<CalendarSubscription>('/CalendarEvents/subscriptions', {
            method: 'POST',
            body: JSON.stringify({ url: current.mutation.url }),
          })
          subscriptionIdMap.set(current.mutation.subscriptionId, createdSubscription.id)
          break
        }
        case 'deleteSubscription': {
          const resolvedSubscriptionId =
            subscriptionIdMap.get(current.mutation.subscriptionId) ?? current.mutation.subscriptionId
          await apiFetch(`/CalendarEvents/subscriptions/${resolvedSubscriptionId}`, {
            method: 'DELETE',
          })
          break
        }
      }

      remaining.shift()
      syncedAny = true
    } catch (err) {
      if (
        current.mutation.type !== 'createEvent' &&
        current.mutation.type !== 'createMeal' &&
        current.mutation.type !== 'createSubscription' &&
        err instanceof Error &&
        err.message.toLowerCase().includes('nicht gefunden')
      ) {
        remaining.shift()
        syncedAny = true
        continue
      }

      if (isOfflineError(err)) {
        break
      }

      error.value = err instanceof Error ? err.message : 'Offline-Sync fuer Dashboard fehlgeschlagen.'
      break
    }
  }

  writePendingMutations(remaining)
  isSyncing.value = false

  if (syncedAny) {
    success.value = 'Offline-Aenderungen im Dashboard wurden synchronisiert.'
    await loadDashboardData()
  }
}

async function handleOnlineDashboardSync() {
  isOnline.value = true
  if (typeof navigator !== 'undefined' && !navigator.onLine) {
    return
  }

  await syncPendingDashboardMutations()
}

function handleOfflineDashboardMode() {
  isOnline.value = false
}

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
  try {
    const data = await apiFetch<CalendarSubscription[]>('/CalendarEvents/subscriptions')
    calendarSubscriptions.value = data
    persistCalendarSubscriptions()
  } catch (err) {
    const cached = readOfflineCache<CalendarSubscription[]>('dashboard:calendar-subscriptions')
    if (!cached) {
      throw err
    }

    calendarSubscriptions.value = cached.value
    offlineSnapshotAt.value = offlineSnapshotAt.value ?? cached.updatedAt
  }
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
    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Kalender-Abo konnte nicht gespeichert werden.'
      return
    }

    const tempSubscriptionId = makeTempId('offline-subscription')
    enqueueOfflineMutation<DashboardMutation>('dashboard', {
      type: 'createSubscription',
      subscriptionId: tempSubscriptionId,
      url,
    })
    refreshPendingMutationCount()

    calendarSubscriptions.value.push({
      id: tempSubscriptionId,
      url,
      createdAt: new Date().toISOString(),
    })
    persistCalendarSubscriptions()
    subscriptionUrl.value = ''
    success.value = 'Kalender-Abo offline gespeichert.'
    return
  }

  subscriptionUrl.value = ''
  success.value = 'Kalender-Abo gespeichert.'
  await loadMonthData()
}

async function removeSubscription(id: string) {
  error.value = ''
  success.value = ''

  if (isTempId(id)) {
    removeQueuedCreateSubscription(id)
    calendarSubscriptions.value = calendarSubscriptions.value.filter((subscription) => subscription.id !== id)
    persistCalendarSubscriptions()
    success.value = 'Lokales Kalender-Abo wurde entfernt.'
    return
  }

  try {
    await apiFetch(`/CalendarEvents/subscriptions/${id}`, { method: 'DELETE' })
    await loadCalendarSubscriptions()
  } catch (err) {
    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Kalender-Abo konnte nicht entfernt werden.'
      return
    }

    enqueueOfflineMutation<DashboardMutation>('dashboard', {
      type: 'deleteSubscription',
      subscriptionId: id,
    })
    refreshPendingMutationCount()
    calendarSubscriptions.value = calendarSubscriptions.value.filter((subscription) => subscription.id !== id)
    persistCalendarSubscriptions()
    success.value = 'Kalender-Abo offline entfernt und zum Sync vorgemerkt.'
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
  const cacheKey = currentMonthCacheKey()
  const subscriptions = calendarSubscriptions.value.map((subscription) => subscription.url)

  try {
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
    offlineSnapshotAt.value = null
    saveOfflineCache(cacheKey, {
      events: events.value,
      mealPlans: mealPlans.value,
    })
  } catch (err) {
    const cached = readOfflineCache<{
      events: CalendarEvent[]
      mealPlans: MealPlan[]
    }>(cacheKey)

    if (!cached) {
      throw err
    }

    events.value = cached.value.events
    mealPlans.value = cached.value.mealPlans
    offlineSnapshotAt.value = cached.updatedAt
    error.value = 'Offline-Modus: Es werden die zuletzt geladenen Kalender- und Meal-Daten angezeigt.'
  }
}

async function loadRecipes() {
  try {
    const data = await apiFetch<Recipe[]>('/recipes')
    recipes.value = data
    saveOfflineCache('dashboard:recipes', data)
  } catch (err) {
    const cached = readOfflineCache<Recipe[]>('dashboard:recipes')
    if (!cached) {
      throw err
    }

    recipes.value = cached.value
    offlineSnapshotAt.value = offlineSnapshotAt.value ?? cached.updatedAt
  }
}

async function loadDashboardData() {
  error.value = ''
  offlineSnapshotAt.value = null

  try {
    await Promise.all([loadCalendarSubscriptions(), loadRecipes()])
    await loadMonthData()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Dashboard konnte nicht geladen werden.'
  }
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
    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Termin konnte nicht gespeichert werden.'
      return
    }

    for (const date of dates) {
      const tempEventId = makeTempId('offline-event')
      enqueueOfflineMutation<DashboardMutation>('dashboard', {
        type: 'createEvent',
        eventId: tempEventId,
        payload: {
          date,
          title,
          startTime: toBackendTimeValue(startTime || ''),
          endTime: toBackendTimeValue(endTime || ''),
          notes: eventForm.value.notes.trim() || null,
        },
      })

      events.value.push({
        id: tempEventId,
        date,
        title,
        startTime: toBackendTimeValue(startTime || ''),
        endTime: toBackendTimeValue(endTime || ''),
        notes: eventForm.value.notes.trim() || null,
      })
    }

    refreshPendingMutationCount()
    refreshDashboardSnapshot()
    showEventModal.value = false
    success.value = dates.length === 1
      ? 'Termin offline gespeichert.'
      : `${dates.length} Termine offline gespeichert.`
    resetEventForm()
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

  if (isTempId(id)) {
    removeQueuedCreateEvent(id)
    events.value = events.value.filter((event) => event.id !== id)
    refreshDashboardSnapshot()
    success.value = 'Lokaler Termin wurde entfernt.'
    return
  }

  try {
    await apiFetch(`/CalendarEvents/${id}`, { method: 'DELETE' })
  } catch (err) {
    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Termin konnte nicht gelöscht werden.'
      return
    }

    enqueueOfflineMutation<DashboardMutation>('dashboard', {
      type: 'deleteEvent',
      eventId: id,
    })
    refreshPendingMutationCount()
    events.value = events.value.filter((event) => event.id !== id)
    refreshDashboardSnapshot()
    success.value = 'Termin offline entfernt und zum Sync vorgemerkt.'
    return
  }
  success.value = 'Termin gelöscht.'
  await loadMonthData()
}

async function createMeal() {
  error.value = ''
  success.value = ''
  const payload: MealPayload = {
    date: selectedDate.value,
    mealType: mealForm.value.mealType,
    servings: mealForm.value.servings,
    notes: mealForm.value.notes || null,
    recipeId: mealForm.value.recipeId,
  }

  try {
    await apiFetch('/mealPlans', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  } catch (err) {
    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Meal konnte nicht gespeichert werden.'
      return
    }

    const tempMealId = makeTempId('offline-meal')
    enqueueOfflineMutation<DashboardMutation>('dashboard', {
      type: 'createMeal',
      mealId: tempMealId,
      payload,
    })
    refreshPendingMutationCount()

    mealPlans.value.push({
      id: tempMealId,
      date: payload.date,
      mealType: payload.mealType,
      servings: payload.servings,
      notes: payload.notes ?? null,
      isCompleted: false,
      recipeId: payload.recipeId,
      recipeName: getRecipeName(payload.recipeId),
    })
    refreshDashboardSnapshot()

    mealForm.value = {
      recipeId: '',
      mealType: 'Dinner',
      servings: 1,
      notes: '',
    }

    showMealModal.value = false
    success.value = 'Meal offline gespeichert.'
    return
  }

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

  if (isTempId(id)) {
    removeQueuedCreateMeal(id)
    mealPlans.value = mealPlans.value.filter((meal) => meal.id !== id)
    refreshDashboardSnapshot()
    success.value = 'Lokales Meal wurde entfernt.'
    return
  }

  try {
    await apiFetch(`/MealPlans/${id}`, {
      method: 'DELETE',
    })
  } catch (err) {
    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Meal konnte nicht gelöscht werden.'
      return
    }

    enqueueOfflineMutation<DashboardMutation>('dashboard', {
      type: 'deleteMeal',
      mealId: id,
    })
    refreshPendingMutationCount()
    mealPlans.value = mealPlans.value.filter((meal) => meal.id !== id)
    refreshDashboardSnapshot()
    success.value = 'Meal offline entfernt und zum Sync vorgemerkt.'
    return
  }

  success.value = 'Meal gelöscht.'
  await loadMonthData()
}

async function completeMeal(id: string) {
  error.value = ''
  success.value = ''

  if (isTempId(id)) {
    enqueueOfflineMutation<DashboardMutation>('dashboard', {
      type: 'completeMeal',
      mealId: id,
    })
    refreshPendingMutationCount()
    mealPlans.value = mealPlans.value.map((meal) =>
      meal.id === id ? { ...meal, isCompleted: true } : meal,
    )
    refreshDashboardSnapshot()
    success.value = 'Meal offline als erledigt markiert.'
    return
  }

  try {
    await apiFetch(`/MealPlans/${id}/complete`, {
      method: 'POST',
    })
  } catch (err) {
    if (!isOfflineError(err)) {
      error.value = err instanceof Error ? err.message : 'Meal konnte nicht abgeschlossen werden.'
      return
    }

    enqueueOfflineMutation<DashboardMutation>('dashboard', {
      type: 'completeMeal',
      mealId: id,
    })
    refreshPendingMutationCount()
    mealPlans.value = mealPlans.value.map((meal) =>
      meal.id === id ? { ...meal, isCompleted: true } : meal,
    )
    refreshDashboardSnapshot()
    success.value = 'Meal offline als erledigt markiert und zum Sync vorgemerkt.'
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
  error.value = ''

  try {
    await loadMonthData()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Kalenderdaten konnten nicht geladen werden.'
  }
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
  refreshPendingMutationCount()
  window.addEventListener('online', handleOnlineDashboardSync)
  window.addEventListener('offline', handleOfflineDashboardMode)
  resetEventForm()
  await loadDashboardData()
  await syncPendingDashboardMutations()
})

onUnmounted(() => {
  window.removeEventListener('online', handleOnlineDashboardSync)
  window.removeEventListener('offline', handleOfflineDashboardMode)
})
</script>

<style scoped>
/* Bootstrap hat keine 7-Spalten-Utility – minimales Grid für den Kalender. */
.calendar-weekdays,
.calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 8px;
}
.calendar-weekdays span {
  text-align: center;
}
.calendar-day {
  min-height: 110px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  cursor: pointer;
}
.calendar-day.selected {
  box-shadow: inset 0 0 0 2px var(--bs-primary);
}
@media (max-width: 540px) {
  .calendar-day {
    min-height: 70px;
  }
}
</style>
