// Shared display formatters for the Loads pages — avoids re-deriving the
// same date/weight string logic in the dashboard table, detail page, etc.
export function formatDate(isoDate) {
  return new Date(`${isoDate}T00:00:00`).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })
}

export function formatWeight(weightKg) {
  return `${weightKg.toLocaleString('en-LK')} kg`
}
