import { InvoiceStatus } from '../../../lib/enums.js'

/**
 * Returns the semantic design system tone corresponding to the invoice status.
 * Maps to StatusBadge tone classes ('blue', 'green', 'amber', 'neutral', 'red').
 */
export function getInvoiceStatusTone(status) {
  switch (status) {
    case InvoiceStatus.PAID:
      return 'green'
    case InvoiceStatus.ISSUED:
      return 'blue'
    case InvoiceStatus.PAYMENT_PENDING:
      return 'amber'
    case InvoiceStatus.DRAFT:
      return 'neutral'
    case InvoiceStatus.VOID:
    case InvoiceStatus.FAILED:
      return 'red'
    default:
      return 'neutral'
  }
}

export const STATUS_FILTER_OPTIONS = [
  { value: 'ALL', label: 'All Statuses' },
  { value: InvoiceStatus.DRAFT, label: 'Draft' },
  { value: InvoiceStatus.ISSUED, label: 'Issued' },
  { value: InvoiceStatus.PAYMENT_PENDING, label: 'Payment Pending' },
  { value: InvoiceStatus.PAID, label: 'Paid' },
  { value: InvoiceStatus.FAILED, label: 'Failed' },
  { value: InvoiceStatus.VOID, label: 'Void' },
]

export const DATE_RANGE_OPTIONS = [
  { value: 'all', label: 'All Time' },
  { value: '7d', label: 'Last 7 Days' },
  { value: '30d', label: 'Last 30 Days' },
  { value: '90d', label: 'Last 90 Days' },
]
