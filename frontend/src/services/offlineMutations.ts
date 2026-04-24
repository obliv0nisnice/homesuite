type MutationEnvelope<T> = {
  id: string
  createdAt: string
  mutation: T
}

function isBrowserAvailable() {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined'
}

function getStorageKey(scope: string) {
  return `homesuite:offline-mutations:${scope}`
}

export function makeTempId(prefix: string) {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`
}

export function readOfflineMutations<T>(scope: string): Array<MutationEnvelope<T>> {
  if (!isBrowserAvailable()) {
    return []
  }

  try {
    const raw = window.localStorage.getItem(getStorageKey(scope))
    if (!raw) {
      return []
    }

    const parsed = JSON.parse(raw) as Array<MutationEnvelope<T>>
    return Array.isArray(parsed) ? parsed : []
  } catch (error) {
    console.warn(`Offline mutation read failed for ${scope}`, error)
    return []
  }
}

export function writeOfflineMutations<T>(scope: string, entries: Array<MutationEnvelope<T>>) {
  if (!isBrowserAvailable()) {
    return
  }

  try {
    window.localStorage.setItem(getStorageKey(scope), JSON.stringify(entries))
  } catch (error) {
    console.warn(`Offline mutation write failed for ${scope}`, error)
  }
}

export function enqueueOfflineMutation<T>(scope: string, mutation: T) {
  const entries = readOfflineMutations<T>(scope)
  entries.push({
    id: makeTempId(`mutation-${scope}`),
    createdAt: new Date().toISOString(),
    mutation,
  })
  writeOfflineMutations(scope, entries)
}
