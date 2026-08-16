import { cx } from '../lib/cx.js'

// Follows DESIGN.md > Components > Status Badges: bold Source Sans 3 text
// on a light tinted background. Color is semantic only — never decorative
// (DESIGN.md > Brand & Style > "Semantic Color Logic").
//
// Domain-free by design (.claude/rules/frontend-design.md #1): this takes
// `tone` and `children`, never a raw enum value — it has no idea what
// LoadStatus/TripStatus/etc. are. Callers own the status→tone mapping (see
// e.g. src/features/loads/lib/statusTone.js) and pass the label text
// themselves via children.
const TONE_CLASSES = {
  blue: 'bg-status-blue-bg text-status-blue-text',
  amber: 'bg-status-amber-bg text-status-amber-text',
  green: 'bg-status-green-bg text-status-green-text',
  red: 'bg-status-red-bg text-status-red-text',
  // Not a DESIGN.md semantic status color — a neutral chip for states that
  // aren't yet in any of the four workflow-status lanes (e.g. "Draft").
  neutral: 'bg-surface-container text-on-surface-variant',
}

// DESIGN.md > Shapes: badges get a 4px radius (rounded) by default. Pass
// `pill` for the one exception — DESIGN.md reserves full-rounding for a
// "Live"/"In Transit" style indicator; the caller decides when that applies.
function StatusBadge({ tone = 'blue', pill = false, className, children }) {
  return (
    <span
      className={cx(
        'inline-flex items-center px-2 py-0.5 text-status-badge',
        pill ? 'rounded-full' : 'rounded',
        TONE_CLASSES[tone],
        className,
      )}
    >
      {children}
    </span>
  )
}

export default StatusBadge
