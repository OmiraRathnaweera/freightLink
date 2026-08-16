import axios from 'axios'
import { store } from '../../store/index.js'
import { refreshAccessToken, logout } from '../../features/auth/store/authSlice.js'

/**
 * Configured Axios instance for every API call in the app. `baseURL` must
 * include the full path prefix (e.g. `https://api.example.com/api/v1`) —
 * see `docs/api-contract-openapi-skeleton.md` Section 2 for the `/api/v1`
 * convention. Set `VITE_API_BASE_URL` in `.env`/`.env.local` (not committed
 * here — see CLAUDE.md's rule against reading/writing `.env*` files).
 */
export const axiosClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

/**
 * Flattens the API's documented error envelope
 * (`{ error: { code, message, details } }`, api-contract Section 2.2) into
 * a plain Error with `.status`/`.code`/`.details` attached, so every
 * caller (api.js helpers, TanStack Query's `retry`/`onError`) sees one
 * consistent shape regardless of whether the server returned a conforming
 * body, a non-JSON body, or the request never reached the server at all.
 */
function normalizeError(error) {
  const envelope = error.response?.data?.error
  const normalized = new Error(envelope?.message ?? error.message ?? 'Something went wrong. Please try again.')
  normalized.status = error.response?.status
  normalized.code = envelope?.code
  normalized.details = envelope?.details
  return normalized
}

axiosClient.interceptors.request.use((config) => {
  const { accessToken } = store.getState().auth
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }
  return config
})

axiosClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    if (error.response?.status === 401 && originalRequest && !originalRequest._retry) {
      originalRequest._retry = true
      try {
        const { accessToken } = await store.dispatch(refreshAccessToken()).unwrap()
        originalRequest.headers.Authorization = `Bearer ${accessToken}`
        return axiosClient(originalRequest)
      } catch {
        store.dispatch(logout())
      }
    }

    return Promise.reject(normalizeError(error))
  },
)
