import { cx } from '../../../lib/cx.js'
import { InvoiceStatus } from '../lib/invoiceStatus.js'

const STATUS_STYLES = {
  [InvoiceStatus.PAID]: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-400',
  [InvoiceStatus.ISSUED]: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800 dark:bg-blue-950/40 dark:text-blue-400',
  [InvoiceStatus.PAYMENT_PENDING]: 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-400',
  [InvoiceStatus.DRAFT]: 'border-slate-300 bg-slate-100 text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300',
  [InvoiceStatus.FAILED]: 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-800 dark:bg-rose-950/40 dark:text-rose-400',
  [InvoiceStatus.OVERDUE]: 'border-red-200 bg-red-50 text-red-700 dark:border-red-800 dark:bg-red-950/40 dark:text-red-400',
}

/**
 * Renders a crisp status pill badge matching the Invoice Ledger design reference.
 */
function InvoiceStatusBadge({ status, className }) {
  const style = STATUS_STYLES[status] || 'border-slate-200 bg-slate-100 text-slate-700'

  return (
    <span
      className={cx(
        'inline-flex items-center rounded border px-2.5 py-0.5 text-[12px] font-medium tracking-tight whitespace-nowrap',
        style,
        className,
      )}
    >
      {status}
    </span>
  )
}

export default InvoiceStatusBadge
