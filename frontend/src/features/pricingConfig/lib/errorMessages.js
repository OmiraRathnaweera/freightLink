const PRICING_CONFIG_ERROR_MESSAGES = {
  FUEL_PRICE_RATE_NOT_FOUND: 'This fuel rate no longer exists.',
  FUEL_PRICE_RATE_ALREADY_DELETED: 'This fuel rate was already deleted.',
  VEHICLE_CLASS_EFFICIENCY_NOT_FOUND: 'This efficiency tier no longer exists.',
  VEHICLE_CLASS_EFFICIENCY_ALREADY_DELETED: 'This efficiency tier was already deleted.',
  VEHICLE_CLASS_EFFICIENCY_INVALID_PAYLOAD_BAND: 'Max payload must be greater than min payload for this band.',
  VEHICLE_CLASS_EFFICIENCY_INVALID_VOLUME_BAND: 'Max volume must be greater than min volume for this band.',
  VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP: 'This band overlaps another vehicle class’s current payload or volume range.',
  VEHICLE_CLASS_EFFICIENCY_BAND_GAP:
    'This band leaves a gap against the other vehicle classes’ current payload or volume ranges.',
  PRICING_FORMULA_CONFIG_NOT_FOUND: 'This pricing formula configuration no longer exists.',
  PRICING_FORMULA_CONFIG_ALREADY_DELETED: 'This pricing formula configuration was already deleted.',
  PRICING_CONFIG_MISSING: 'No pricing configuration exists for this yet.',
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
