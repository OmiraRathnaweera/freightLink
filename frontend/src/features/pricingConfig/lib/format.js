// Small local formatters, duplicated from src/features/loads/lib/format.js's
// logic rather than imported — this feature doesn't touch the loads folder.
export function formatDateTime(isoDateTime) {
  return new Date(isoDateTime).toLocaleString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function formatCurrency(amount) {
  if (amount == null) return '—'
  return `LKR ${amount.toLocaleString('en-LK', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

// marginPercent is stored as a fraction (0.15 = 15%), not a 0-100 percent.
export function formatPercent(fraction) {
  if (fraction == null) return '—'
  return `${(fraction * 100).toFixed(1)}%`
}

// Converts a native <input type="datetime-local">'s value to a full ISO
// datetime string for the API's effectiveFrom field.
export function fromDateTimeLocalInput(localDateTime) {
  if (!localDateTime) return ''
  return new Date(localDateTime).toISOString()
}
