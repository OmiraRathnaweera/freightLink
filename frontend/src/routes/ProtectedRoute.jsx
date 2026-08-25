import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAppSelector } from '../hooks/useAppSelector.js'
import { isRouteAllowedForRole } from '../features/auth/lib/roleAccess.js'

// Route protection reads exclusively from Redux auth state — never Context —
// because the same isAuthenticated/role check has to run for every route,
// independent of whichever component tree happens to be mounted.
// (See ADR: state-management strategy.)
//
// This is a *layout route* (data-router pattern): it renders <Outlet />
// when the check passes, so it wraps a group of child <Route>s rather than
// a single page. See src/routes/AppRoutes.jsx for usage — a single
// instance wraps the whole DashboardLayout subtree; there's no per-route
// `allowedRoles` prop to manage. Role authorization is instead looked up
// by the *current URL* against the centralized
// src/features/auth/lib/roleAccess.js map, so adding a new route's role
// access never requires touching this component or AppRoutes.jsx's guard
// nesting — just that one map.
function ProtectedRoute() {
  const { isAuthenticated, role } = useAppSelector((state) => state.auth)
  const location = useLocation()

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (!isRouteAllowedForRole(role, location.pathname)) {
    return <Navigate to="/unauthorized" replace />
  }

  return <Outlet />
}

export default ProtectedRoute
