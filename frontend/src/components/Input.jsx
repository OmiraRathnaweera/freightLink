import { forwardRef } from 'react'
import { cx } from '../lib/cx.js'

// Follows DESIGN.md > Components > Input Fields & Forms: 1px hairline
// border (Slate-300), 6px radius, 15px text; focus shifts the border to
// Primary Navy with a 2px Slate-200 outer glow.
//
// Pass `mono` for the "Monospace Fields" case — money (LKR), distances
// (km), and weights (kg) must render their value in JetBrains Mono.
//
// Pass `error` for a failed-validation field (not in DESIGN.md's written
// spec, but implied by the Post-a-Load validation-error Stitch screen):
// swaps the border/focus-ring to the semantic red status color.
const Input = forwardRef(function Input({ className, mono = false, error = false, ...props }, ref) {
  return (
    <input
      ref={ref}
      className={cx(
        'w-full rounded-md border bg-white px-3 py-2 text-[15px] text-on-surface',
        'placeholder:text-on-surface-variant',
        'disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-on-surface-variant',
        error
          ? 'border-status-red-text focus:border-status-red-text focus:outline-none focus:ring-2 focus:ring-status-red-bg'
          : 'border-slate-300 focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border',
        mono && 'font-mono',
        className,
      )}
      {...props}
    />
  )
})

export default Input
