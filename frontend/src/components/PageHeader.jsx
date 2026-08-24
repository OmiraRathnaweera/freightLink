import { cx } from '../lib/cx.js'

// Page-level header: optional label-caps eyebrow, headline-lg title,
// optional body-md description, optional right-aligned actions (e.g. a
// primary Button). Typography per DESIGN.md's `typography` tokens.
function PageHeader({ eyebrow, title, description, actions, className }) {
  return (
    <div
      className={cx(
        'flex flex-wrap items-start justify-between gap-4 border-b border-slate-border pb-4',
        className,
      )}
    >
      <div>
        {eyebrow && <p className="mb-1 text-label-caps text-on-surface-variant">{eyebrow}</p>}
        <h1 className="text-headline-lg text-on-surface">{title}</h1>
        {description && <p className="mt-1 text-body-md text-on-surface-variant">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  )
}

export default PageHeader
