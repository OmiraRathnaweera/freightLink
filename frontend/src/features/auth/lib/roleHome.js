import { UserRole } from '../../../lib/enums.js'

// Where to send a user right after login (or when a signed-in user hits a
// PublicRoute like /login). No dedicated Admin dashboard route exists yet,
// so Admin falls back to /loads like Shipper.
const ROLE_HOME_PATHS = {
  [UserRole.SHIPPER]: '/loads',
  [UserRole.ADMIN]: '/loads',
  [UserRole.AGENCY_STAFF]: '/agencies',
  [UserRole.DRIVER]: '/unauthorized',
}

export function getRoleHomePath(role) {
  return ROLE_HOME_PATHS[role] ?? '/loads'
}
