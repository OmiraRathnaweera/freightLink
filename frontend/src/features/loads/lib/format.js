// Shared display formatters for the Loads pages — avoids re-deriving the
// same date/weight/currency string logic in the dashboard table, detail
// page, etc.
export function formatDate(isoDate) {
  return new Date(`${isoDate}T00:00:00`).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })
}

// For full ISO 8601 timestamps (createdAt, updatedAt, pickupWindowStart/End)
// — formatDate above expects a bare date and would double-append a time.
export function formatDateTime(isoDateTime) {
  return new Date(isoDateTime).toLocaleString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function formatWeight(weightKg) {
  return `${weightKg.toLocaleString('en-LK')} kg`
}

// estimatedPrice is nullable until a pricing-estimate endpoint exists
// (docs/load-management-api.md Section 3.6) — renders an em dash until then.
export function formatCurrency(amount) {
  if (amount == null) return '—'
  return `LKR ${amount.toLocaleString('en-LK', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

// Converts a full ISO datetime (from the API) to the "YYYY-MM-DDTHH:mm"
// format the native <input type="datetime-local"> expects/produces.
export function toDateTimeLocalInput(isoDateTime) {
  if (!isoDateTime) return ''
  const date = new Date(isoDateTime)
  const pad = (value) => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

// Converts a datetime-local input's value back to a full ISO datetime
// string for the API's pickupWindowStart/End fields.
export function fromDateTimeLocalInput(localDateTime) {
  if (!localDateTime) return ''
  return new Date(localDateTime).toISOString()
}

// For the file preview panel's byte-size footer (LoadFileResponseDto.bytes).
export function formatFileSize(bytes) {
  if (bytes == null) return ''
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

// Single place the shipperName fallback lives, so the loads table and the
// detail page can never drift apart. There's no /users/{id} endpoint to
// resolve a name from — shipperName is either present on the Load response
// or it isn't, never fetched separately.
export function formatShipperName(shipperName, shipperUserId) {
  if (shipperName && shipperName.trim()) return shipperName
  if (shipperUserId) return `Unknown (${shipperUserId.slice(0, 8)}…)`
  return 'Unknown'
}
