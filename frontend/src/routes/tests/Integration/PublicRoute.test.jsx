import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { Provider } from 'react-redux'
import PublicRoute from '../../PublicRoute.jsx'
import { UserRole } from '../../../lib/enums.js'
import { createTestStore } from '../../../test/testUtils.jsx'

// PublicRoute is ProtectedRoute's inverse (see its own file comment): it
// wraps sign-out-only routes like /login and bounces an already-authenticated
// user to their role home (features/auth/lib/roleHome.js) instead of letting
// them see the login form again.
function renderPublic({ authState, initialEntries = ['/login'] } = {}) {
  const store = createTestStore(authState)
  render(
    <Provider store={store}>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route path="/loads" element={<div>Shipper Home</div>} />
          <Route path="/agencies" element={<div>Agency Staff Home</div>} />
          <Route path="/unauthorized" element={<div>Unauthorized Page</div>} />
          <Route element={<PublicRoute />}>
            <Route path="/login" element={<div>Login Page</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </Provider>,
  )
}

describe('PublicRoute — unauthenticated visitors see the guarded page', () => {
  it('renders the login form for an unauthenticated visitor', () => {
    renderPublic({ authState: { isAuthenticated: false, role: null } })
    expect(screen.getByText('Login Page')).toBeInTheDocument()
  })
})

describe('PublicRoute — already-authenticated users are bounced to their role home', () => {
  it('redirects a signed-in Shipper from /login to /loads', () => {
    renderPublic({ authState: { isAuthenticated: true, role: UserRole.SHIPPER } })
    expect(screen.getByText('Shipper Home')).toBeInTheDocument()
    expect(screen.queryByText('Login Page')).not.toBeInTheDocument()
  })

  it('redirects a signed-in Agency Staff user from /login to /agencies', () => {
    renderPublic({ authState: { isAuthenticated: true, role: UserRole.AGENCY_STAFF } })
    expect(screen.getByText('Agency Staff Home')).toBeInTheDocument()
    expect(screen.queryByText('Login Page')).not.toBeInTheDocument()
  })

  it('redirects a signed-in Driver from /login to /unauthorized (Driver has no web home)', () => {
    renderPublic({ authState: { isAuthenticated: true, role: UserRole.DRIVER } })
    expect(screen.getByText('Unauthorized Page')).toBeInTheDocument()
    expect(screen.queryByText('Login Page')).not.toBeInTheDocument()
  })
})
