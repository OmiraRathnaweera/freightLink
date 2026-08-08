import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAppSelector } from '../hooks/useAppSelector.js'

// Route protection reads exclusively from Redux auth state — never Context —
// because the same isAuthenticated/role check has to run for every route,
// independent of whichever component tree happens to be mounted.
// (See ADR: state-management strategy.)
//
// This is a *layout route* (data-router pattern): it renders <Outlet />
// when the check passes, so it wraps a group of child <Route>s rather than
// a single page. See src/routes/AppRoutes.jsx for usage, e.g.:
//
//   {
//     element: <ProtectedRoute allowedRoles={[UserRole.ADMIN]} />,
//     children: [{ path: '/agent-workflows', element: <AgentWorkflowConsolePage /> }],
//   }
//
// allowedRoles is optional — omit it to just require any authenticated user.
function ProtectedRoute({ allowedRoles }) {
  const { isAuthenticated, role } = useAppSelector((state) => state.auth)
  const location = useLocation()

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (allowedRoles && !allowedRoles.includes(role)) {
    // TODO: add an /unauthorized page once real pages exist.
    return <Navigate to="/unauthorized" replace />
  }

  return <Outlet />
}

export default ProtectedRoute
