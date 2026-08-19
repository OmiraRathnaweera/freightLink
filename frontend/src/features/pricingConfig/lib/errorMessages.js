const PRICING_CONFIG_ERROR_MESSAGES = {
  FUEL_PRICE_RATE_NOT_FOUND: 'This fuel rate no longer exists.',
  FUEL_PRICE_RATE_ALREADY_DELETED: 'This fuel rate was already deleted.',
  VEHICLE_CLASS_EFFICIENCY_NOT_FOUND: 'This efficiency tier no longer exists.',
  VEHICLE_CLASS_EFFICIENCY_ALREADY_DELETED: 'This efficiency tier was already deleted.',
  VEHICLE_CLASS_EFFICIENCY_INVALID_PAYLOAD_BAND: 'The payload band is invalid — the lowest tier must start at 0 kg.',
  VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP: 'This payload band overlaps another vehicle class’s current band.',
  VEHICLE_CLASS_EFFICIENCY_BAND_GAP: 'This payload band leaves a gap against the other vehicle classes’ current bands.',
  PRICING_CONFIG_MISSING: 'No pricing configuration exists for this yet — add a fuel rate or efficiency tier below.',
  VALIDATION_ERROR: 'Please fix the highlighted fields.',
}

export function getPricingConfigErrorMessage(error) {
  return PRICING_CONFIG_ERROR_MESSAGES[error?.code] ?? error?.message ?? 'Something went wrong. Please try again.'
}

// Turns a VALIDATION_ERROR's `details: [{ field, issue }]` into a Formik setErrors-shaped object.
export function mapValidationDetailsToFormik(details) {
  if (!Array.isArray(details)) return {}
  return details.reduce((errors, detail) => {
    if (detail?.field) errors[detail.field] = detail.issue
    return errors
  }, {})
}
