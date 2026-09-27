import { LoadProposalStatus } from '../../../lib/enums.js'

// StatusBadge (src/components/StatusBadge.jsx) is domain-free — it only
// knows about `tone`, never LoadProposalStatus. This is where that
// enum→tone knowledge actually lives, mirroring statusTone.js's pattern
// for LoadStatus.
const LOAD_PROPOSAL_STATUS_TONE = {
  [LoadProposalStatus.PENDING]: 'amber',
  [LoadProposalStatus.ACCEPTED]: 'green',
  [LoadProposalStatus.REJECTED]: 'red',
  [LoadProposalStatus.WITHDRAWN]: 'neutral',
}

export function getLoadProposalStatusTone(status) {
  return LOAD_PROPOSAL_STATUS_TONE[status] ?? 'neutral'
}
