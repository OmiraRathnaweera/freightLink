import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ProtectedRoute from '../../ProtectedRoute.jsx'
import { UserRole } from '../../../lib/enums.js'
import { createTestStore } from '../../../test/testUtils.jsx'
import { Provider } from 'react-redux'

// ProtectedRoute is a layout route — it renders <Outlet/> on success or
// <Navigate/> otherwise (see its own file comment) — so these tests build a
// minimal route tree per case: the guarded route under test, plus stand-ins
// for /login and /unauthorized to land on and assert against.
function renderProtected(path, { authState, initialEntries = [path] } = {}) {
  const store = createTestStore(authState)
  render(
    <Provider store={store}>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route path="/login" element={<div>Login Page</div>} />
          <Route path="/unauthorized" element={<div>Unauthorized Page</div>} />
          <Route element={<ProtectedRoute />}>
            <Route path={path} element={<div>Protected Content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </Provider>,
  )
}

describe('ProtectedRoute — unauthenticated users are redirected to login', () => {
  it.each(['/loads', '/loads/new', '/loads/abc-123', '/loads/abc-123/edit'])(
    'redirects an unauthenticated visitor to %s to /login',
    (path) => {
      renderProtected(path, { authState: { isAuthenticated: false, role: null } })
      expect(screen.getByText('Login Page')).toBeInTheDocument()
      expect(screen.queryByText('Protected Content')).not.toBeInTheDocument()
    },
  )
})

describe('ProtectedRoute — authenticated Shipper can access load routes', () => {
  it.each(['/loads', '/loads/new', '/loads/abc-123', '/loads/abc-123/edit'])('allows a Shipper to reach %s', (path) => {
    renderProtected(path, { authState: { isAuthenticated: true, role: UserRole.SHIPPER } })
    expect(screen.getByText('Protected Content')).toBeInTheDocument()
  })
})

describe('ProtectedRoute — role guard blocks roles without access', () => {
  it('redirects a Driver away from /loads to /unauthorized', () => {
    renderProtected('/loads', { authState: { isAuthenticated: true, role: UserRole.DRIVER } })
    expect(screen.getByText('Unauthorized Page')).toBeInTheDocument()
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument()
  })

  it('allows an Admin to reach /loads (Admin is granted /loads access)', () => {
    renderProtected('/loads', { authState: { isAuthenticated: true, role: UserRole.ADMIN } })
    expect(screen.getByText('Protected Content')).toBeInTheDocument()
  })
})

// The following three blocks close a gap: earlier tests only exercised
// /loads*. These prove the same guard against the other role-gated prefixes
// in features/auth/lib/roleAccess.js's ROLE_ALLOWED_PREFIXES map.

describe('ProtectedRoute — /agencies is Agency Staff/Admin only', () => {
  it.each([UserRole.AGENCY_STAFF, UserRole.ADMIN])('allows %s to reach /agencies', (role) => {
    renderProtected('/agencies', { authState: { isAuthenticated: true, role } })
    expect(screen.getByText('Protected Content')).toBeInTheDocument()
  })

  it.each([UserRole.SHIPPER, UserRole.DRIVER])('redirects %s away from /agencies to /unauthorized', (role) => {
    renderProtected('/agencies', { authState: { isAuthenticated: true, role } })
    expect(screen.getByText('Unauthorized Page')).toBeInTheDocument()
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument()
  })
})

describe('ProtectedRoute — bare /trips (the Agency Staff trips list) is Agency Staff/Admin only', () => {
  it.each([UserRole.AGENCY_STAFF, UserRole.ADMIN])('allows %s to reach /trips', (role) => {
    renderProtected('/trips', { authState: { isAuthenticated: true, role } })
    expect(screen.getByText('Protected Content')).toBeInTheDocument()
  })

  it.each([UserRole.SHIPPER, UserRole.DRIVER])('redirects %s away from bare /trips to /unauthorized', (role) => {
    renderProtected('/trips', { authState: { isAuthenticated: true, role } })
    expect(screen.getByText('Unauthorized Page')).toBeInTheDocument()
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument()
  })
})

describe('ProtectedRoute — a Shipper is granted trip-detail pages ("/trips/", trailing slash) but not the bare list', () => {
  it('allows a Shipper to reach /trips/abc-123 (their own dispatched trip)', () => {
    renderProtected('/trips/abc-123', { authState: { isAuthenticated: true, role: UserRole.SHIPPER } })
    expect(screen.getByText('Protected Content')).toBeInTheDocument()
  })
})

describe('ProtectedRoute — /billing is open to every web role except Driver', () => {
  it.each([UserRole.SHIPPER, UserRole.AGENCY_STAFF, UserRole.ADMIN])('allows %s to reach /billing', (role) => {
    renderProtected('/billing', { authState: { isAuthenticated: true, role } })
    expect(screen.getByText('Protected Content')).toBeInTheDocument()
  })

  it('redirects a Driver away from /billing to /unauthorized (Requirement 1: Driver is mobile-only)', () => {
    renderProtected('/billing', { authState: { isAuthenticated: true, role: UserRole.DRIVER } })
    expect(screen.getByText('Unauthorized Page')).toBeInTheDocument()
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument()
  })
})
