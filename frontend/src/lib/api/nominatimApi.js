const NOMINATIM_BASE_URL = 'https://nominatim.openstreetmap.org'

/**
 * Plain `fetch` client for OpenStreetMap's public Nominatim geocoding API —
 * deliberately not routed through `axiosClient.js`, since that instance is
 * configured for our own backend (baseURL, auth interceptor, refresh-on-401)
 * and Nominatim is an unrelated third-party service with no auth of its own.
 *
 * Search is biased to Sri Lanka (`countrycodes=lk`) since FreightLink is an
 * SL freight platform. Callers are expected to debounce and gate on a
 * minimum query length (see `useLocationSearch`) to stay within reasonable
 * client-side use of Nominatim's public endpoint.
 */

/**
 * @param {string} query
 * @param {{ signal?: AbortSignal }} [options]
 * @returns {Promise<{ displayName: string, lat: number, lng: number }[]>}
 */
export async function searchPlaces(query, { signal } = {}) {
  const url = `${NOMINATIM_BASE_URL}/search?format=jsonv2&limit=5&countrycodes=lk&q=${encodeURIComponent(query)}`
  const response = await fetch(url, { signal, headers: { Accept: 'application/json' } })
  if (!response.ok) {
    throw new Error('Location search failed')
  }
  const results = await response.json()
  return results.map((result) => ({
    displayName: result.display_name,
    lat: Number(result.lat),
    lng: Number(result.lon),
  }))
}

/**
 * @param {number} lat
 * @param {number} lng
 * @param {{ signal?: AbortSignal }} [options]
 * @returns {Promise<{ displayName: string }>}
 */
export async function reverseGeocode(lat, lng, { signal } = {}) {
  const url = `${NOMINATIM_BASE_URL}/reverse?format=jsonv2&lat=${lat}&lon=${lng}`
  const response = await fetch(url, { signal, headers: { Accept: 'application/json' } })
  if (!response.ok) {
    throw new Error('Reverse geocoding failed')
  }
  const result = await response.json()
  if (!result || !result.display_name) {
    throw new Error('Reverse geocoding returned no address')
  }
  return { displayName: result.display_name }
}
