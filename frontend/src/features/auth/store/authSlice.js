import { createSlice, createAsyncThunk } from '@reduxjs/toolkit'

// Auth lives in Redux Toolkit, never in Context, because it's read from
// many unrelated places at once — route guards, the API client's request
// interceptor, the header/nav, feature pages across every role — and it
// must survive navigation between routes. Context re-renders/scopes to a
// subtree by design, which is the wrong shape for session state that's
// truly global. (See ADR: state-management strategy.)

const initialState = {
  user: null, // { id, email, name, ... }
  accessToken: null, // short-lived JWT (5 min)
  refreshToken: null, // long-lived token (30 days)
  role: null, // one of UserRole from ../../../lib/enums.js
  isAuthenticated: false,
  status: 'idle', // 'idle' | 'loading' | 'succeeded' | 'failed'
  error: null,
}

// --- Async thunks (placeholders only — no real API calls yet) ---

// credentials: { email, password }
// resolves: { user, accessToken, refreshToken, role }
export const login = createAsyncThunk('auth/login', async (credentials) => {
  // TODO: return apiClient.post('/auth/login', credentials)
  throw new Error(`login thunk not implemented yet: ${JSON.stringify(credentials)}`)
})

export const logout = createAsyncThunk('auth/logout', async () => {
  // TODO: apiClient.post('/auth/logout'); clear persisted tokens from storage
})

// resolves: { accessToken, refreshToken }
export const refreshAccessToken = createAsyncThunk(
  'auth/refreshAccessToken',
  async (_, { getState }) => {
    const { refreshToken } = getState().auth
    // TODO: return apiClient.post('/auth/refresh', { refreshToken })
    throw new Error(`refreshAccessToken thunk not implemented yet: ${refreshToken}`)
  },
)

// Rehydrates session on app start from persisted storage.
// resolves: { user, accessToken, refreshToken, role } | null
export const loadUserFromStorage = createAsyncThunk('auth/loadUserFromStorage', async () => {
  // TODO: read tokens from storage (e.g. localStorage) and validate/decode,
  // or call GET /auth/me with the stored access token
  return null
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
        state.user = action.payload?.user ?? null
        state.accessToken = action.payload?.accessToken ?? null
        state.refreshToken = action.payload?.refreshToken ?? null
        state.role = action.payload?.role ?? null
        state.isAuthenticated = true
      })
      .addCase(login.rejected, (state, action) => {
        state.status = 'failed'
        state.error = action.error.message
      })

      // logout — reset to a fresh initialState regardless of outcome
      .addCase(logout.fulfilled, () => initialState)
      .addCase(logout.rejected, () => initialState)

      // refreshAccessToken
      .addCase(refreshAccessToken.fulfilled, (state, action) => {
        state.accessToken = action.payload?.accessToken ?? state.accessToken
        state.refreshToken = action.payload?.refreshToken ?? state.refreshToken
      })
      .addCase(refreshAccessToken.rejected, (state, action) => {
        // A failed refresh means the session can no longer be trusted.
        Object.assign(state, initialState, { error: action.error.message })
      })

      // loadUserFromStorage
      .addCase(loadUserFromStorage.pending, (state) => {
        state.status = 'loading'
      })
      .addCase(loadUserFromStorage.fulfilled, (state, action) => {
        state.status = 'succeeded'
        if (action.payload) {
          state.user = action.payload.user ?? null
          state.accessToken = action.payload.accessToken ?? null
          state.refreshToken = action.payload.refreshToken ?? null
          state.role = action.payload.role ?? null
          state.isAuthenticated = Boolean(action.payload.accessToken)
        }
      })
      .addCase(loadUserFromStorage.rejected, (state, action) => {
        state.status = 'failed'
        state.error = action.error.message
      })
  },
})

export const { clearAuthError } = authSlice.actions
export default authSlice.reducer
