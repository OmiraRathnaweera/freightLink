import { cx } from '../lib/cx.js'

// Follows DESIGN.md > Components > Buttons.
// - primary: Navy background, white text, 6px radius — uses the primary/
//   on-primary token pair (see index.css re: the primary vs primary-container
//   hex ambiguity in DESIGN.md; primary/on-primary was chosen as the
//   canonical accessible pair).
// - secondary: white background, 1px Slate-300 border, Navy text.
// - status: reserved for modal actions (e.g. Approve/Reject) per DESIGN.md
//   ("Status Buttons — Only used in modals; themed according to the status
//   color, e.g. 'Approve' is a Green-600 button"); pass `status` alongside
//   variant="status".
const VARIANT_CLASSES = {
  primary: 'border border-primary bg-primary text-on-primary hover:bg-primary/90',
  secondary: 'border border-slate-300 bg-white text-primary hover:bg-slate-100',
}

const STATUS_CLASSES = {
  blue: 'border border-status-blue-text bg-status-blue-text text-white hover:opacity-90',
  amber: 'border border-status-amber-text bg-status-amber-text text-white hover:opacity-90',
  green: 'border border-status-green-text bg-status-green-text text-white hover:opacity-90',
  red: 'border border-status-red-text bg-status-red-text text-white hover:opacity-90',
}

function Button({ variant = 'primary', status, type = 'button', className, children, ...props }) {
  const variantClasses = variant === 'status' ? STATUS_CLASSES[status] : VARIANT_CLASSES[variant]

  return (
    <button
      type={type}
      className={cx(
        'inline-flex items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition-colors',
        'disabled:cursor-not-allowed disabled:opacity-50',
        variantClasses,
        className,
      )}
      {...props}
    >
      {children}
    </button>
  )
}

export default Button
