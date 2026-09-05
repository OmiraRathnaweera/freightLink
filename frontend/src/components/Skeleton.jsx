import { cx } from '../lib/cx.js'

// A single pulsing placeholder block — compose several into a table-row or
// card skeleton for a page's loading state.
function Skeleton({ className }) {
  return <div className={cx('animate-pulse rounded bg-surface-container', className)} />
}

export default Skeleton
