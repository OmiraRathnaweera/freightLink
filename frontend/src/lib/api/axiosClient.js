import axios from 'axios'
import { clearPersistedRefreshToken } from '../../features/auth/lib/tokenStorage.js'

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

// store/index.js and authSlice.js both transitively import this file
// (authSlice.js -> authApi.js -> api.js -> axiosClient.js), so importing
// them statically here creates a real circular dependency. Depending on
// which module the app happens to touch first elsewhere, that cycle can
// resolve in an order where store/index.js reads authSlice's default
// export before authSlice.js has finished initializing it — exactly the
// "Cannot access 'authReducer' before initialization" crash this file
// used to cause once DashboardLayout.jsx started importing `logout` from
// authSlice.js (which made authSlice.js the first-touched module instead
// of store/index.js). Dynamic import() defers resolution past the
// synchronous module-init phase entirely, sidestepping the cycle
// regardless of import order elsewhere in the app.
async function getAuthModules() {
  const [{ store }, authSlice] = await Promise.all([
    import('../../store/index.js'),
    import('../../features/auth/store/authSlice.js'),
  ])
  return { store, refreshAccessToken: authSlice.refreshAccessToken, clearAuth: authSlice.clearAuth }
}

axiosClient.interceptors.request.use(async (config) => {
  const { store } = await getAuthModules()
  const { accessToken } = store.getState().auth
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }
  return config
})

// /auth/login and /auth/refresh are excluded from the refresh-retry branch
// below — a 401 from either of those IS the failure (wrong credentials, or
// a truly-dead refresh token), not a stale-access-token case that a
// refresh could fix. Without this check, a failed login would trigger a
// pointless extra /auth/refresh call before failing anyway.
function isAuthEndpoint(url) {
  return typeof url === 'string' && (url.includes('/auth/login') || url.includes('/auth/refresh'))
}

axiosClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    if (
      error.response?.status === 401 &&
      originalRequest &&
      !originalRequest._retry &&
      !isAuthEndpoint(originalRequest.url)
    ) {
      originalRequest._retry = true
      const { store, refreshAccessToken, clearAuth } = await getAuthModules()
      try {
        const { accessToken } = await store.dispatch(refreshAccessToken()).unwrap()
        originalRequest.headers.Authorization = `Bearer ${accessToken}`
        return axiosClient(originalRequest)
      } catch {
        // Dispatch the synchronous clearAuth reducer, not the async logout
        // thunk — logout() would call authApi.logout() through this same
        // axios instance with an already-dead token, 401-ing again and
        // re-entering this interceptor on a fresh (non-_retry) request,
        // looping. Any mounted ProtectedRoute reacts to isAuthenticated
        // flipping false and redirects to /login on its own — no
        // imperative navigation needed here.
        store.dispatch(clearAuth())
        clearPersistedRefreshToken()
      }
    }

    return Promise.reject(normalizeError(error))
  },
)
