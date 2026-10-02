import { AgencyStatus } from '../../../lib/enums.js'

// Mirrors the backend's AgencyStatusTransitionRules.AllowedTransitions
// (backend/Common/Domain/AgencyStatusTransitionRules.cs) — the server is the source of truth and
// re-validates every change; this only decides which actions the admin UI offers. No status is
// terminal: Suspended can always be reversed (issue #56).
export const ALLOWED_AGENCY_TRANSITIONS = Object.freeze({
  [AgencyStatus.PENDING]: [AgencyStatus.VERIFIED, AgencyStatus.SUSPENDED],
  [AgencyStatus.VERIFIED]: [AgencyStatus.ACTIVE, AgencyStatus.SUSPENDED],
  [AgencyStatus.ACTIVE]: [AgencyStatus.SUSPENDED],
  [AgencyStatus.SUSPENDED]: [AgencyStatus.ACTIVE, AgencyStatus.VERIFIED, AgencyStatus.PENDING],
})

export function getAllowedNextStatuses(status) {
  return ALLOWED_AGENCY_TRANSITIONS[status] ?? []
}

export function canTransitionAgency(from, to) {
  return getAllowedNextStatuses(from).includes(to)
}
