import { useEffect, useState } from 'react'
import { searchPlaces } from '../lib/api/nominatimApi.js'
import { useDebouncedValue } from './useDebouncedValue.js'

const MIN_QUERY_LENGTH = 3

// UI-only hook backing LocationPicker's search box: debounces `query`,
// looks it up via Nominatim, and cancels the in-flight request whenever a
// newer query supersedes it (fast typing, or the box getting cleared).
export function useLocationSearch(query) {
  const debouncedQuery = useDebouncedValue(query, 400)
  const trimmedQuery = debouncedQuery.trim()
  const belowMinLength = trimmedQuery.length < MIN_QUERY_LENGTH
  const [results, setResults] = useState([])
  const [isSearching, setIsSearching] = useState(false)

  useEffect(() => {
    if (belowMinLength) return

    const controller = new AbortController()
    Promise.resolve()
      .then(() => setIsSearching(true))
      .then(() => searchPlaces(trimmedQuery, { signal: controller.signal }))
      .then((places) => setResults(places))
      .catch((error) => {
        if (error.name !== 'AbortError') setResults([])
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsSearching(false)
      })

    return () => controller.abort()
  }, [trimmedQuery, belowMinLength])

  return {
    results: belowMinLength ? [] : results,
    isSearching: belowMinLength ? false : isSearching,
  }
}
