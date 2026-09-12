import { AlertTriangle } from 'lucide-react'
import { cx } from '../lib/cx.js'
import Button from './Button.jsx'

// Generic "failed to load" state — a red-toned counterpart to EmptyState,
// with a retry action instead of a create action.
function ErrorState({ title = 'Connection Error', description, onRetry, className }) {
  return (
    <div className={cx('flex flex-col items-center gap-3 px-6 py-16 text-center', className)}>
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-status-red-bg">
        <AlertTriangle className="h-6 w-6 text-status-red-text" strokeWidth={1.5} />
      </div>
      <h3 className="text-headline-md text-on-surface">{title}</h3>
      {description && <p className="max-w-sm text-body-md text-on-surface-variant">{description}</p>}
      {onRetry && (
        <Button variant="secondary" onClick={onRetry} className="mt-2">
          Retry
        </Button>
      )}
    </div>
  )
}

export default ErrorState
