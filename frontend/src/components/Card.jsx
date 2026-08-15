import { cx } from '../lib/cx.js'

// The "card surface" base style per DESIGN.md > Elevation & Depth /
// Components > Cards: pure white surface, 6px radius, 1px Slate-200
// hairline border, the one soft shadow token, minimal 16px padding.
function Card({ className, children, ...props }) {
  return (
    <div
      className={cx(
        'rounded-md border border-slate-border bg-surface-container-lowest p-4 shadow-soft',
        className,
      )}
      {...props}
    >
      {children}
    </div>
  )
}

export default Card
