import { describe, expect, it, beforeEach } from 'vitest'
import { UserRole } from '../../../lib/enums.js'
import { isRouteAllowedForRole } from '../lib/roleAccess.js'
import { getRoleHomePath } from '../lib/roleHome.js'
import authReducer, { setUser, clearAuth } from '../store/authSlice.js'
import {
  persistRefreshToken,
  getPersistedRefreshToken,
  clearPersistedRefreshToken,
} from '../lib/tokenStorage.js'

describe('Cross-Platform Auth & Session Consistency (Y3S01-103)', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  describe('1. Role Enum Parity with Backend & Mobile', () => {
    it('defines exactly the 4 standard system roles with matching casing', () => {
      expect(UserRole.SHIPPER).toBe('Shipper')
      expect(UserRole.AGENCY_STAFF).toBe('AgencyStaff')
      expect(UserRole.DRIVER).toBe('Driver')
      expect(UserRole.ADMIN).toBe('Admin')
    })
  })

  describe('2. Contract Parity: CurrentUserResponseDto to Redux State', () => {
    const backendCurrentUserDto = {
      userId: 'c1000000-0000-0000-0000-000000000001',
      email: 'shipper.test@freightlink.lk',
      fullName: 'Shipper Representative',
      phoneE164: '+94771234567',
      role: 'Shipper',
      isActive: true,
      createdAt: '2026-09-01T10:00:00+00:00',
    }

    it('populates user, role, and isAuthenticated identical to mobile AuthUser', () => {
      const initialState = {
        user: null,
        accessToken: null,
        refreshToken: null,
        role: null,
        isAuthenticated: false,
        status: 'idle',
        error: null,
        isBootstrapped: true,
      }

      const stateAfterSetUser = authReducer(initialState, setUser(backendCurrentUserDto))

      expect(stateAfterSetUser.isAuthenticated).toBe(true)
      expect(stateAfterSetUser.role).toBe('Shipper')
      expect(stateAfterSetUser.user).toEqual(backendCurrentUserDto)
      expect(stateAfterSetUser.user.userId).toBe('c1000000-0000-0000-0000-000000000001')
      expect(stateAfterSetUser.user.email).toBe('shipper.test@freightlink.lk')
      expect(stateAfterSetUser.user.isActive).toBe(true)
    })

    it('clears session when setUser(null) is dispatched', () => {
      const authenticatedState = {
        user: backendCurrentUserDto,
        accessToken: 'mock-jwt-token',
        refreshToken: 'mock-refresh-token',
        role: 'Shipper',
        isAuthenticated: true,
        status: 'idle',
        error: null,
        isBootstrapped: true,
      }

      const stateAfterClear = authReducer(authenticatedState, setUser(null))
      expect(stateAfterClear.isAuthenticated).toBe(false)
      expect(stateAfterClear.role).toBeNull()
      expect(stateAfterClear.user).toBeNull()
    })
  })

  describe('3. Role-Based Route Gating Parity', () => {
    it('enforces Shipper permissions and role home (/loads)', () => {
      expect(getRoleHomePath(UserRole.SHIPPER)).toBe('/loads')

      expect(isRouteAllowedForRole(UserRole.SHIPPER, '/loads')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.SHIPPER, '/loads/new')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.SHIPPER, '/billing')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.SHIPPER, '/agent-workflows')).toBe(true)

      // Gated from Agency operations and Admin pricing
      expect(isRouteAllowedForRole(UserRole.SHIPPER, '/agencies')).toBe(false)
      expect(isRouteAllowedForRole(UserRole.SHIPPER, '/pricing-config')).toBe(false)
    })

    it('enforces AgencyStaff permissions and role home (/agencies)', () => {
      expect(getRoleHomePath(UserRole.AGENCY_STAFF)).toBe('/agencies')

      expect(isRouteAllowedForRole(UserRole.AGENCY_STAFF, '/agencies')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.AGENCY_STAFF, '/trips')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.AGENCY_STAFF, '/billing')).toBe(true)

      // Gated from Shipper load creation and Admin pricing
      expect(isRouteAllowedForRole(UserRole.AGENCY_STAFF, '/loads')).toBe(false)
      expect(isRouteAllowedForRole(UserRole.AGENCY_STAFF, '/pricing-config')).toBe(false)
      expect(isRouteAllowedForRole(UserRole.AGENCY_STAFF, '/agent-workflows')).toBe(false)
    })

    it('enforces Driver permissions and role home (/trips)', () => {
      expect(getRoleHomePath(UserRole.DRIVER)).toBe('/trips')

      expect(isRouteAllowedForRole(UserRole.DRIVER, '/trips')).toBe(true)

      // Gated from all other modules
      expect(isRouteAllowedForRole(UserRole.DRIVER, '/loads')).toBe(false)
      expect(isRouteAllowedForRole(UserRole.DRIVER, '/agencies')).toBe(false)
      expect(isRouteAllowedForRole(UserRole.DRIVER, '/billing')).toBe(false)
      expect(isRouteAllowedForRole(UserRole.DRIVER, '/pricing-config')).toBe(false)
      expect(isRouteAllowedForRole(UserRole.DRIVER, '/agent-workflows')).toBe(false)
    })

    it('enforces Admin permissions across all management routes', () => {
      expect(getRoleHomePath(UserRole.ADMIN)).toBe('/loads')

      expect(isRouteAllowedForRole(UserRole.ADMIN, '/loads')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.ADMIN, '/agencies')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.ADMIN, '/trips')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.ADMIN, '/billing')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.ADMIN, '/agent-workflows')).toBe(true)
      expect(isRouteAllowedForRole(UserRole.ADMIN, '/pricing-config')).toBe(true)
    })
  })

  describe('4. Session & Token Storage Cleanup on Logout/401', () => {
    it('persists and clears refresh tokens in storage correctly', () => {
      persistRefreshToken('sample-refresh-token')
      expect(getPersistedRefreshToken()).toBe('sample-refresh-token')

      clearPersistedRefreshToken()
      expect(getPersistedRefreshToken()).toBeNull()
    })

    it('clearAuth synchronously resets auth state on 401 response interceptor trigger', () => {
      const activeState = {
        user: { userId: 'usr-1', email: 'test@example.com', role: 'Shipper' },
        accessToken: 'active-jwt-token',
        refreshToken: 'active-refresh-token',
        role: 'Shipper',
        isAuthenticated: true,
        status: 'succeeded',
        error: null,
        isBootstrapped: true,
      }

      const resetState = authReducer(activeState, clearAuth())

      expect(resetState.isAuthenticated).toBe(false)
      expect(resetState.user).toBeNull()
      expect(resetState.accessToken).toBeNull()
      expect(resetState.refreshToken).toBeNull()
      expect(resetState.role).toBeNull()
      expect(resetState.isBootstrapped).toBe(true)
    })
  })
})
