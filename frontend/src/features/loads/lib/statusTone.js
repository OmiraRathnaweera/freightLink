import { LoadStatus } from '../../../lib/enums.js'

// StatusBadge (src/components/StatusBadge.jsx) is domain-free — it only
// knows about `tone`, never LoadStatus. This is where that enum→tone
// knowledge actually lives (.claude/rules/frontend-design.md #1). Only
// covers the tones DESIGN.md names explicitly; extend as new statuses need
// a badge rather than inventing a color outside the four semantic tones.
const LOAD_STATUS_TONE = {
  [LoadStatus.DRAFT]: 'neutral',
  [LoadStatus.POSTED]: 'blue',
  [LoadStatus.MATCHED]: 'blue',
  [LoadStatus.IN_TRANSIT]: 'amber',
  [LoadStatus.DELIVERED]: 'green',
  [LoadStatus.CLOSED]: 'neutral',
  [LoadStatus.CANCELLED]: 'red',
}

export function getLoadStatusTone(status) {
  return LOAD_STATUS_TONE[status] ?? 'blue'
}
