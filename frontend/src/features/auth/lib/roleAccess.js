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
  // '/trips/' (trailing slash, not bare '/trips') deliberately grants only
  // trip-detail pages, not the Agency Staff trips list — a Shipper can view
  // their own load's dispatched trip once they have its id (from
  // LoadResponseDto.tripId), but GET /trips (list) is Agency/Driver/Admin
  // only on the backend regardless.
  [UserRole.SHIPPER]: ['/loads', '/billing', '/agent-workflows', '/trips/'],
  [UserRole.AGENCY_STAFF]: ['/loads', '/agencies', '/trips', '/billing'],
  [UserRole.DRIVER]: [], // Requirement 1: drivers only have access to mobile application UI
  [UserRole.ADMIN]: ['/loads', '/agencies', '/trips', '/billing', '/pricing-config', '/disputes', '/analytics'],
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
