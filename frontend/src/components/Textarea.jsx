import { forwardRef } from 'react'
import { cx } from '../lib/cx.js'

// Same recipe as Input (DESIGN.md > Input Fields & Forms), for multi-line
// fields like a cargo description.
const Textarea = forwardRef(function Textarea({ className, error = false, ...props }, ref) {
  return (
    <textarea
      ref={ref}
      className={cx(
        'w-full rounded-md border bg-white px-3 py-2 text-[15px] text-on-surface',
        'placeholder:text-on-surface-variant',
        'disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-on-surface-variant',
        error
          ? 'border-status-red-text focus:border-status-red-text focus:outline-none focus:ring-2 focus:ring-status-red-bg'
          : 'border-slate-300 focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border',
        className,
      )}
      {...props}
    />
  )
})

export default Textarea
