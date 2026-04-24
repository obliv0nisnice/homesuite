type CacheEnvelope<T> = {
  updatedAt: string
  value: T
}

function isBrowserAvailable() {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined'
}

export function saveOfflineCache<T>(key: string, value: T) {
  if (!isBrowserAvailable()) {
    return
  }

  const payload: CacheEnvelope<T> = {
    updatedAt: new Date().toISOString(),
    value,
  }

  try {
    window.localStorage.setItem(key, JSON.stringify(payload))
  } catch (error) {
    console.warn(`Offline cache write failed for ${key}`, error)
  }
}

export function readOfflineCache<T>(key: string): CacheEnvelope<T> | null {
  if (!isBrowserAvailable()) {
    return null
  }

  try {
    const raw = window.localStorage.getItem(key)
    if (!raw) {
      return null
    }

    const parsed = JSON.parse(raw) as CacheEnvelope<T>
    if (!parsed || typeof parsed.updatedAt !== 'string' || !('value' in parsed)) {
      return null
    }

    return parsed
  } catch (error) {
    console.warn(`Offline cache read failed for ${key}`, error)
    return null
  }
}
