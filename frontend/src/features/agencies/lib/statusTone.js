import { AgencyStatus, ComplianceDocStatus } from '../../../lib/enums.js'

// Maps an Agency's onboarding status to StatusBadge's accepted semantic
// tones ('neutral' | 'blue' | 'amber' | 'green' | 'red').
const AGENCY_STATUS_TONE = {
  [AgencyStatus.PENDING]: 'amber',
  [AgencyStatus.VERIFIED]: 'blue',
  [AgencyStatus.ACTIVE]: 'green',
  [AgencyStatus.SUSPENDED]: 'red',
}

export function getAgencyStatusTone(status) {
  return AGENCY_STATUS_TONE[status] ?? 'neutral'
}

// Maps a ComplianceDoc's review status to StatusBadge's accepted tones.
const COMPLIANCE_DOC_STATUS_TONE = {
  [ComplianceDocStatus.PENDING]: 'amber',
  [ComplianceDocStatus.VERIFIED]: 'green',
  [ComplianceDocStatus.REJECTED]: 'red',
  [ComplianceDocStatus.EXPIRED]: 'neutral',
}

export function getComplianceDocStatusTone(status) {
  return COMPLIANCE_DOC_STATUS_TONE[status] ?? 'neutral'
}
