import { createSlice, createAsyncThunk } from '@reduxjs/toolkit'
import * as authApi from '../api/authApi.js'
import { getPersistedRefreshToken, persistRefreshToken, clearPersistedRefreshToken } from '../lib/tokenStorage.js'

// Auth lives in Redux Toolkit, never in Context, because it's read from
// many unrelated places at once — route guards, the API client's request
// interceptor, the header/nav, feature pages across every role — and it
// must survive navigation between routes. Context re-renders/scopes to a
// subtree by design, which is the wrong shape for session state that's
// truly global. (See ADR: state-management strategy.)

const initialState = {
  user: null, // { id, email, name, role, ... } — from GET /auth/me, never the login response
  accessToken: null, // short-lived JWT (5 min) — memory only, never persisted
  refreshToken: null, // long-lived token (30 days) — mirrored to localStorage, see lib/tokenStorage.js
  role: null, // one of UserRole from ../../../lib/enums.js, derived from `user`
  isAuthenticated: false, // only true once `user` is set — tokens alone don't count (see setUser)
  status: 'idle', // 'idle' | 'loading' | 'succeeded' | 'failed' — login/logout/refresh only
  error: null,
  isBootstrapped: false, // has the app-start session-restore check finished? (separate from `status`)
}

// --- Thunks ---

// credentials: { email, password }
// resolves: { accessToken, refreshToken, user }
export const login = createAsyncThunk('auth/login', async (credentials, { dispatch }) => {
  const tokens = await authApi.login(credentials) // POST /auth/login -> { accessToken, refreshToken } only
  dispatch(setTokens(tokens))
  persistRefreshToken(tokens.refreshToken)
  // The access token must already be in the store before this call, since
  // axiosClient's request interceptor reads it from state — hence
  // dispatching setTokens above before calling getCurrentUser here.
  const user = await authApi.getCurrentUser() // GET /auth/me — never trust the login response for profile/role
  return { ...tokens, user }
})

export const logout = createAsyncThunk('auth/logout', async (_, { getState }) => {
  // Captured before any client state is cleared — /auth/logout requires
  // the refresh token being revoked in its body; the endpoint has no way
  // to identify which token to revoke otherwise.
  const { refreshToken } = getState().auth
  try {
    if (refreshToken) {
      await authApi.logout(refreshToken)
    }
  } catch {
    // Best-effort — the session is ending client-side regardless.
  }
  clearPersistedRefreshToken()
})

// Refresh tokens rotate/are single-use (docs: "rotates the old one"), so
// two concurrent refresh attempts sharing the same pre-rotation token is a
// real race — the second one fails against an already-invalidated token,
// and its `.rejected` reducer would then wipe out the *new* token the
// first request just persisted. This happens in practice: React
// StrictMode (main.jsx) double-invokes App.jsx's bootstrap effect in dev,
// firing `refreshAccessToken` twice back-to-back; concurrent 401s from
// multiple in-flight API calls could trigger the same race in production.
// A single shared in-flight promise means every concurrent caller gets the
// same one real network call instead of racing each other.
let refreshPromise = null

// resolves: { accessToken, refreshToken }
export const refreshAccessToken = createAsyncThunk(
  'auth/refreshAccessToken',
  async (_, { getState }) => {
    if (refreshPromise) {
      return refreshPromise
    }
    const { refreshToken } = getState().auth
    if (!refreshToken) {
      throw new Error('No refresh token available')
    }
    refreshPromise = authApi
      .refresh(refreshToken)
      .then((tokens) => {
        persistRefreshToken(tokens.refreshToken)
        return tokens
      })
      .finally(() => {
        refreshPromise = null
      })
    return refreshPromise
  },
)

// Rehydrates a session on app start from the persisted refresh token.
// resolves: { accessToken, refreshToken, user } | null
export const bootstrapAuth = createAsyncThunk('auth/bootstrapAuth', async (_, { dispatch }) => {
  const refreshToken = getPersistedRefreshToken()
  if (!refreshToken) {
    return null
  }
  dispatch(setTokens({ accessToken: null, refreshToken }))
  try {
    const tokens = await dispatch(refreshAccessToken()).unwrap()
    const user = await authApi.getCurrentUser()
    return { ...tokens, user }
  } catch {
    // Expired/invalid refresh token — not an error state, just "not logged in".
    clearPersistedRefreshToken()
    return null
  }
})

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    // Synchronous escape hatch for UI-only concerns, e.g. dismissing a
    // stale error banner without a round trip.
    clearAuthError(state) {
      state.error = null
    },
    setTokens(state, action) {
      state.accessToken = action.payload.accessToken
      state.refreshToken = action.payload.refreshToken
    },
    setUser(state, action) {
      state.user = action.payload
      state.role = action.payload?.role ?? null
      state.isAuthenticated = Boolean(action.payload)
    },
    // Synchronous reset — used by axiosClient's response interceptor when a
    // refresh attempt fails, instead of dispatching the async `logout`
    // thunk (which would itself call the API through the same axios
    // instance with an already-dead token, 401-ing again and re-entering
    // the interceptor — see axiosClient.js's comment).
    clearAuth(state) {
      Object.assign(state, initialState, { isBootstrapped: state.isBootstrapped })
    },
  },
  extraReducers: (builder) => {
    builder
      // login
      .addCase(login.pending, (state) => {
        state.status = 'loading'
        state.error = null
      })
      .addCase(login.fulfilled, (state, action) => {
        state.status = 'succeeded'
        authSlice.caseReducers.setUser(state, { payload: action.payload.user })
      })
      .addCase(login.rejected, (state, action) => {
        state.status = 'failed'
        state.error = action.error.message
      })

      // logout — reset to a fresh initialState regardless of outcome
      .addCase(logout.fulfilled, (state) => {
        Object.assign(state, initialState, { isBootstrapped: state.isBootstrapped })
      })
      .addCase(logout.rejected, (state) => {
        Object.assign(state, initialState, { isBootstrapped: state.isBootstrapped })
      })

      // refreshAccessToken
      .addCase(refreshAccessToken.fulfilled, (state, action) => {
        state.accessToken = action.payload.accessToken
        state.refreshToken = action.payload.refreshToken
      })
      .addCase(refreshAccessToken.rejected, (state, action) => {
        // A failed refresh means the session can no longer be trusted.
        clearPersistedRefreshToken()
        Object.assign(state, initialState, { error: action.error.message, isBootstrapped: state.isBootstrapped })
      })

      // bootstrapAuth
      .addCase(bootstrapAuth.fulfilled, (state, action) => {
        state.isBootstrapped = true
        state.status = 'succeeded'
        if (action.payload) {
          authSlice.caseReducers.setUser(state, { payload: action.payload.user })
        }
      })
      .addCase(bootstrapAuth.rejected, (state, action) => {
        state.isBootstrapped = true
        state.status = 'failed'
        state.error = action.error.message
      })
  },
})

export const { clearAuthError, setTokens, setUser, clearAuth } = authSlice.actions
export default authSlice.reducer
