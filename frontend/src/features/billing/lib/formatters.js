/**
 * Format a numeric amount to formatted LKR currency string (e.g. "45,000.00" or "LKR 45,000.00").
 */
export function formatCurrency(amount, includePrefix = false) {
  if (amount == null || isNaN(amount)) return '0.00'
  const formatted = Number(amount).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
  return includePrefix ? `LKR ${formatted}` : formatted
}

/**
 * Formats an ISO or YYYY-MM-DD date into "MMM dd, yyyy" (e.g. "Oct 24, 2023").
 */
export function formatInvoiceDate(dateString) {
  if (!dateString) return '—'
  const date = new Date(dateString)
  if (isNaN(date.getTime())) return dateString

  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}

/**
 * Formats a date-time into "MMM dd, yyyy HH:mm" (e.g. "Oct 24, 2023 14:30").
 */
export function formatInvoiceDateTime(dateString) {
  if (!dateString) return '—'
  const date = new Date(dateString)
  if (isNaN(date.getTime())) return dateString

  const datePart = date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
  const timePart = date.toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
  return `${datePart} ${timePart}`
}
