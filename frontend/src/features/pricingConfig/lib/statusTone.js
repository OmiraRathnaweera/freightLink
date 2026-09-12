// "Current" is a set-membership question (the max-EffectiveFrom, non-deleted
// row per fuel type/vehicle class) rather than something derivable from a
// single row in isolation — callers compute `isCurrent` and pass it in.
export function getPricingRowTone({ deletedAt, isCurrent }) {
  if (deletedAt) return 'red'
  if (isCurrent) return 'green'
  return 'neutral'
}

export function getPricingRowLabel({ deletedAt, isCurrent }) {
  if (deletedAt) return 'Deleted'
  if (isCurrent) return 'Current'
  return 'Superseded'
}
