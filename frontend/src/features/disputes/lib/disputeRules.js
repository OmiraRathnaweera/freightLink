/**
 * Dispute lifecycle rules and state machine logic (Ticket Y3S01-81).
 * Lifecycle: Raised -> UnderReview -> Resolved.
 * Progression is strictly sequential; no skipping states.
 */

export const DisputeStatus = Object.freeze({
  RAISED: 'Raised',
  UNDER_REVIEW: 'UnderReview',
  RESOLVED: 'Resolved',
})

export const DisputeOutcome = Object.freeze({
  UPHELD: 'Upheld',
  PARTIALLY_UPHELD: 'PartiallyUpheld',
  REJECTED: 'Rejected',
})

export const OUTCOME_OPTIONS = [
  { value: DisputeOutcome.UPHELD, label: 'Upheld (Claim Approved)' },
  { value: DisputeOutcome.PARTIALLY_UPHELD, label: 'Partially Upheld (Split Liability)' },
  { value: DisputeOutcome.REJECTED, label: 'Rejected (Claim Denied)' },
]

/**
 * Validates whether a dispute status can transition to 'UnderReview'.
 * Only disputes in 'Raised' can transition to 'UnderReview'.
 */
export function canStartReview(status) {
  return status === DisputeStatus.RAISED
}

/**
 * Validates whether a dispute can be resolved.
 * Must be in 'UnderReview' before it can be closed/resolved.
 */
export function canResolveDispute(status) {
  return status === DisputeStatus.UNDER_REVIEW
}

/**
 * Checks if a dispute is resolved (terminal read-only state).
 */
export function isDisputeResolved(status) {
  return status === DisputeStatus.RESOLVED
}

/**
 * Maps dispute status to FreightLink design system tone tokens:
 * - Raised: Warning / Amber
 * - UnderReview: In-progress / Blue
 * - Resolved: Success / Green
 */
export function getStatusTone(status) {
  switch (status) {
    case DisputeStatus.RAISED:
      return 'amber'
    case DisputeStatus.UNDER_REVIEW:
      return 'blue'
    case DisputeStatus.RESOLVED:
      return 'green'
    default:
      return 'neutral'
  }
}

export function getStatusLabel(status) {
  switch (status) {
    case DisputeStatus.RAISED:
      return 'Raised'
    case DisputeStatus.UNDER_REVIEW:
      return 'Under Review'
    case DisputeStatus.RESOLVED:
      return 'Resolved'
    default:
      return status
  }
}

/**
 * Validates resolution payload before allowing submission.
 * Strictly requires a non-empty resolution note.
 */
export function validateResolutionPayload(note) {
  if (!note || typeof note !== 'string' || note.trim().length === 0) {
    return 'Resolution note is strictly mandatory and cannot be empty.'
  }
  if (note.trim().length < 10) {
    return 'Resolution note must provide meaningful justification (at least 10 characters).'
  }
  return null
}
