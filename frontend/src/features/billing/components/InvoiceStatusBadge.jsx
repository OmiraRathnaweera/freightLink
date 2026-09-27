import { cx } from '../../../lib/cx.js'
import { InvoiceStatus } from '../lib/invoiceStatus.js'

const STATUS_STYLES = {
  [InvoiceStatus.PAID]: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  [InvoiceStatus.ISSUED]: 'border-blue-200 bg-blue-50 text-blue-700',
  [InvoiceStatus.PAYMENT_PENDING]: 'border-amber-200 bg-amber-50 text-amber-800',
  [InvoiceStatus.DRAFT]: 'border-slate-300 bg-slate-100 text-slate-700',
  [InvoiceStatus.VOIDED]: 'border-rose-200 bg-rose-50 text-rose-700',
  [InvoiceStatus.VOID]: 'border-rose-200 bg-rose-50 text-rose-700',
  [InvoiceStatus.FAILED]: 'border-rose-200 bg-rose-50 text-rose-700',
  [InvoiceStatus.OVERDUE]: 'border-red-200 bg-red-50 text-red-700',
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
