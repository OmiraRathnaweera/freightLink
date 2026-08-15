import { Check } from 'lucide-react'
import { cx } from '../../../lib/cx.js'

// Vertical status stepper for Load Detail (Stitch screens 2/4/6/7 each show
// a version of this — collapsed here into one reusable component). Steps
// before `currentIndex` render done (green check), the step at
// `currentIndex` renders active (or as an error/interrupt when
// `isErrorAtCurrent`, for the Needs Review state), steps after render
// pending/greyed.
function WorkflowTimeline({ steps, currentIndex, isErrorAtCurrent = false }) {
  return (
    <ol className="space-y-0">
      {steps.map((label, index) => {
        const isDone = index < currentIndex
        const isCurrent = index === currentIndex
        const isLast = index === steps.length - 1

        return (
          <li key={label} className="relative flex gap-3 pb-6 last:pb-0">
            {!isLast && (
              <span
                className={cx('absolute left-[11px] top-6 h-full w-px', isDone ? 'bg-status-green-text' : 'bg-slate-border')}
              />
            )}
            <span
              className={cx(
                'relative flex h-6 w-6 shrink-0 items-center justify-center rounded-full',
                isDone && 'bg-status-green-text text-white',
                isCurrent && !isErrorAtCurrent && 'bg-status-blue-text text-white',
                isCurrent && isErrorAtCurrent && 'bg-status-red-text text-white',
                !isDone && !isCurrent && 'bg-surface-container text-on-surface-variant',
              )}
            >
              {isDone ? <Check className="h-3.5 w-3.5" strokeWidth={2} /> : <span className="h-2 w-2 rounded-full bg-current" />}
            </span>
            <span
              className={cx(
                'pt-0.5 text-body-md',
                isCurrent && !isErrorAtCurrent && 'font-semibold text-status-blue-text',
                isCurrent && isErrorAtCurrent && 'font-semibold text-status-red-text',
                !isCurrent && (isDone ? 'text-on-surface' : 'text-on-surface-variant'),
              )}
            >
              {label}
            </span>
          </li>
        )
      })}
    </ol>
  )
}

export default WorkflowTimeline
