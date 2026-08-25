const REFRESH_TOKEN_KEY = 'freightlink.refreshToken'

// Only the refresh token is ever persisted — the access token stays in
// Redux memory only (see authSlice.js's initialState comment). This is the
// pragmatic choice given the backend returns tokens in the JSON response
// body rather than a Set-Cookie header, and changing that is out of scope
// (frontend-only task).
export function getPersistedRefreshToken() {
  return localStorage.getItem(REFRESH_TOKEN_KEY)
}

export function persistRefreshToken(refreshToken) {
  localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
}

export function clearPersistedRefreshToken() {
  localStorage.removeItem(REFRESH_TOKEN_KEY)
}
