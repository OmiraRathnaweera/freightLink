import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ProtectedRoute from '../ProtectedRoute.jsx'
import { UserRole } from '../../lib/enums.js'
import { createTestStore } from '../../test/testUtils.jsx'
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
