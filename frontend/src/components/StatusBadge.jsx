import { cx } from '../lib/cx.js'

// Follows DESIGN.md > Components > Status Badges: bold Source Sans 3 text
// on a light tinted background. Color is semantic only — never decorative
// (DESIGN.md > Brand & Style > "Semantic Color Logic").
//
// `status` should be one of the app's enum values (see src/lib/enums.js
// — LoadStatus, TripStatus, AssignmentStatus, InvoiceStatus, etc.) and is
// rendered as-is as the label. The lookup below only covers the statuses
// DESIGN.md names explicitly; extend it as new statuses need a badge
// rather than inventing a color outside the four semantic tones. Pass
// `tone` directly to override the lookup for an unmapped status.
const STATUS_TONE = {
  Draft: 'neutral',
  Posted: 'blue',
  Matched: 'blue',
  InTransit: 'amber',
  Pending: 'amber',
  Delivered: 'green',
  Approved: 'green',
  Paid: 'green',
  Rejected: 'red',
  Failed: 'red',
  Cancelled: 'red',
}

const TONE_CLASSES = {
  blue: 'bg-status-blue-bg text-status-blue-text',
  amber: 'bg-status-amber-bg text-status-amber-text',
  green: 'bg-status-green-bg text-status-green-text',
  red: 'bg-status-red-bg text-status-red-text',
  // Not a DESIGN.md semantic status color — a neutral chip for states like
  // "Draft" that aren't yet in any of the four workflow-status lanes.
  neutral: 'bg-surface-container text-on-surface-variant',
}

// DESIGN.md > Shapes: badges get a 4px radius (rounded), except "Live" /
// "In Transit" which is the one status reserved for a full pill.
function StatusBadge({ status, tone, className }) {
  const resolvedTone = tone ?? STATUS_TONE[status] ?? 'blue'
  const isLive = status === 'InTransit'

  return (
    <span
      className={cx(
        'inline-flex items-center px-2 py-0.5 text-status-badge',
        isLive ? 'rounded-full' : 'rounded',
        TONE_CLASSES[resolvedTone],
        className,
      )}
    >
      {status}
    </span>
  )
}

export default StatusBadge
