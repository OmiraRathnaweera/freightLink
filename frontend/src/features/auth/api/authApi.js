import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'

// Query key factory — see api-contract-openapi-skeleton.md Section 4.1
// for the two endpoints below.
export const authKeys = {
  all: ['auth'],
  me: () => [...authKeys.all, 'me'],
}

/**
 * POST /auth/login — returns only { accessToken, refreshToken }, never the
 * user profile (the API deliberately keeps these separate; see
 * `getCurrentUser` below).
 * @param {{ email: string, password: string }} credentials
 */
export async function registerShipper(payload) {
  return api.post('/auth/register/shipper', payload)
}

export async function registerAgency(payload) {
  return api.post('/auth/register/agency', payload)
}

export async function login(credentials) {
  return api.post('/auth/login', credentials)
}

/** GET /auth/me — current user's profile + role, read from the access token. */
export async function getCurrentUser() {
  return api.get('/auth/me')
}

/**
 * POST /auth/refresh — exchanges a refresh token for a new access +
 * refresh token pair (rotates the old one).
 */
export async function refresh(refreshToken) {
  return api.post('/auth/refresh', { refreshToken })
}

/** POST /auth/logout — revokes the given refresh token. */
export async function logout(refreshToken) {
  return api.post('/auth/logout', { refreshToken })
}

/** Requests a password-reset email. The API intentionally does not disclose account existence. */
export async function forgotPassword(email) {
  return api.post('/auth/forgot-password', { email })
}

/** Sets a new password using the one-time token delivered by email. */
export async function resetPassword({ token, newPassword }) {
  return api.post('/auth/reset-password', { token, newPassword })
}

/** Verifies an email address using the one-time token delivered by email. */
export async function verifyEmail(token) {
  return api.post('/auth/verify-email', { token })
}

/** Requests another verification email without disclosing account existence. */
export async function resendVerification(email) {
  return api.post('/auth/resend-verification', { email })
}

/**
 * Example login mutation. Deliberately does not dispatch into
 * `authSlice.js` — `POST /auth/login` only returns tokens, so writing a
 * full session (user/role) into Redux needs a follow-up `getCurrentUser()`
 * call too; wiring that end-to-end is left for when a real Login page
 * consumes this (matches `LoginPage.jsx`'s current placeholder submit).
 */
export function useRegisterShipperMutation(options) {
  return useMutation({ mutationFn: registerShipper, ...options })
}

export function useRegisterAgencyMutation(options) {
  return useMutation({ mutationFn: registerAgency, ...options })
}

export function useLoginMutation(options) {
  return useMutation({ mutationFn: login, ...options })
}

/** Example query for the current user's profile. */
export function useCurrentUserQuery(options) {
  return useQuery({ queryKey: authKeys.me(), queryFn: getCurrentUser, ...options })
}

