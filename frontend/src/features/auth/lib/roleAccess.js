import { UserRole } from '../../../lib/enums.js'

// Which route prefixes each role may access. Unlike roleHome.js (one
// post-login landing page per role), a role can have any number of
// entries here. This is the single source of truth for route
// authorization — both ProtectedRoute (src/routes/ProtectedRoute.jsx) and
// DashboardLayout's sidebar filtering read from it, so a route's guard and
// its nav visibility can never drift apart.
//
// To grant a role access to a new route (e.g. a teammate's own module):
// add the route's path prefix to that role's array below. No other file
// needs to change — ProtectedRoute picks it up automatically.
//
//   [UserRole.AGENCY_STAFF]: ['/agencies', '/trips', '/billing', '/invoices'],
//
// Matches docs/api-contract-openapi-skeleton.md's per-resource role matrix
// (Sections 4.2-4.6).
export const ROLE_ALLOWED_PREFIXES = {
  [UserRole.SHIPPER]: ['/loads', '/billing', '/agent-workflows'],
  [UserRole.AGENCY_STAFF]: ['/agencies', '/trips', '/billing'],
  [UserRole.DRIVER]: ['/trips'],
  [UserRole.ADMIN]: ['/loads', '/agencies', '/trips', '/billing', '/agent-workflows', '/pricing-config', '/disputes'],
}

function isPathGated(pathname) {
  return Object.values(ROLE_ALLOWED_PREFIXES)
    .flat()
    .some((prefix) => pathname.startsWith(prefix))
}

// True if `role` may view `pathname`. A path that isn't listed under ANY
// role at all (e.g. /unauthorized) is treated as open to any authenticated
// user, rather than needing a separate exemption list.
export function isRouteAllowedForRole(role, pathname) {
  if (!isPathGated(pathname)) {
    return true
  }
  return (ROLE_ALLOWED_PREFIXES[role] ?? []).some((prefix) => pathname.startsWith(prefix))
}
