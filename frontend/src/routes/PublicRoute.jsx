import { Navigate, Outlet } from 'react-router-dom'
import { useAppSelector } from '../hooks/useAppSelector.js'
import { getRoleHomePath } from '../features/auth/lib/roleHome.js'

// Mirrors ProtectedRoute.jsx's layout-route pattern, inverted: wraps
// routes that only make sense while signed out (/login, /register). An
// already-authenticated user hitting either gets bounced to their role
// home instead of seeing the login form again.
function PublicRoute() {
  const { isAuthenticated, role } = useAppSelector((state) => state.auth)

  if (isAuthenticated) {
    return <Navigate to={getRoleHomePath(role)} replace />
  }

  return <Outlet />
}

export default PublicRoute
