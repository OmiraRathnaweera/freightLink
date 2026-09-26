// User-friendly copy and mapping helpers for agency and fleet vehicle API error envelopes.

const AGENCY_ERROR_MESSAGES = {
  VALIDATION_ERROR: 'Please check the highlighted fields and try again.',
  AGENCY_NOT_ACTIVE: 'Your agency is not active yet. Compliance documents must be verified by an administrator before registering fleet vehicles.',
  AGENCY_NOT_FOUND: 'Agency organization could not be found.',
  FORBIDDEN: 'You are not authorized to perform this fleet management action.',
}

/**
 * Returns a human-friendly error message for an agency or fleet error.
 * @param {object} [error]
 * @returns {string}
 */
export function getAgencyErrorMessage(error) {
  if (error?.status === 409 || error?.code === 'CONFLICT') {
    return 'A vehicle with this registration number already exists in your fleet.'
  }

  const message = error?.message
  if (typeof message === 'string') {
    if (
      message.includes('uq_vehicle_agency_regno') ||
      (message.toLowerCase().includes('duplicate') && message.toLowerCase().includes('registration'))
    ) {
      return 'A vehicle with this registration number already exists in your fleet.'
    }
  }

  return (
    AGENCY_ERROR_MESSAGES[error?.code] ??
    error?.message ??
    'Something went wrong. Please check your inputs and try again.'
  )
}

/**
 * Maps API validation error details ([{ field, issue }]) to Formik's setErrors shape ({ fieldName: errorMessage }).
 * @param {Array<{ field?: string, issue?: string }>} [details]
 * @returns {Record<string, string>}
 */
export function mapValidationDetailsToFormik(details) {
  if (!Array.isArray(details)) return {}
  return details.reduce((errors, detail) => {
    if (detail?.field) {
      // Strips prefixes such as 'request.', 'vehicle.', or '$.'
      const cleanField = detail.field.replace(/^(\$\.|request\.|vehicle\.)/, '')
      const normalizedField = cleanField.charAt(0).toLowerCase() + cleanField.slice(1)
      errors[normalizedField] = detail.issue
    }
    return errors
  }, {})
}
