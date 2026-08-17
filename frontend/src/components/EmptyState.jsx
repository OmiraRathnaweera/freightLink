import { cx } from '../lib/cx.js'

// Generic "nothing here yet" state for any list/table page (My Loads today;
// Agencies/Trips/Billing lists will likely want this too once they're real).
function EmptyState({ icon: Icon, title, description, action, className }) {
  return (
    <div className={cx('flex flex-col items-center gap-3 px-6 py-16 text-center', className)}>
      {Icon && (
        <div className="flex h-12 w-12 items-center justify-center rounded-full bg-surface-container">
          <Icon className="h-6 w-6 text-on-surface-variant" strokeWidth={1.5} />
        </div>
      )}
      <h3 className="text-headline-md text-on-surface">{title}</h3>
      {description && <p className="max-w-sm text-body-md text-on-surface-variant">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}

export default EmptyState
