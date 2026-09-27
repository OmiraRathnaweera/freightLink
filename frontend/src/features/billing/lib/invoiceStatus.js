export const InvoiceStatus = {
  DRAFT: 'Draft',
  ISSUED: 'Issued',
  PAID: 'Paid',
  PAYMENT_PENDING: 'Payment Pending',
  FAILED: 'Failed',
  OVERDUE: 'Overdue',
  VOIDED: 'Voided',
  VOID: 'Void',
}

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
    case InvoiceStatus.VOIDED:
    case InvoiceStatus.VOID:
    case InvoiceStatus.FAILED:
    case InvoiceStatus.OVERDUE:
      return 'red'
    default:
      return 'neutral'
  }
}

export const STATUS_FILTER_OPTIONS = [
  { value: 'ALL', label: 'All Statuses' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Issued', label: 'Issued' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Payment Pending', label: 'Payment Pending' },
  { value: 'Voided', label: 'Voided' },
  { value: 'Failed', label: 'Failed' },
  { value: 'Overdue', label: 'Overdue' },
]

export const DATE_RANGE_OPTIONS = [
  { value: 'all', label: 'All Time' },
  { value: '7d', label: 'Last 7 Days' },
  { value: '30d', label: 'Last 30 Days' },
  { value: '90d', label: 'Last 90 Days' },
]
