// User-friendly copy and mapping helpers for agency and fleet vehicle API error envelopes.

const AGENCY_ERROR_MESSAGES = {
  VALIDATION_ERROR: 'Please check the highlighted fields and try again.',
  AGENCY_NOT_ACTIVE: 'Your agency is not active yet. Compliance documents must be verified by an administrator before registering fleet vehicles.',
  AGENCY_NOT_FOUND: 'Agency organization could not be found.',
  INVALID_COMPLIANCE_DOC_STATUS_TRANSITION: 'This document was already verified by an administrator and can no longer be changed or replaced.',
  INVALID_AGENCY_STATUS_TRANSITION: 'This status change is not allowed from the agency\'s current status. Refresh the page and try again.',
  FORBIDDEN: 'You are not authorized to perform this fleet management action.',
  EMAIL_ALREADY_REGISTERED: 'An account with this email already exists.',
  DRIVER_LICENCE_ALREADY_REGISTERED: 'A driver with this driving licence number is already registered.',
  // Thrown by the update-driver endpoint specifically (as opposed to DRIVER_LICENCE_ALREADY_REGISTERED,
  // thrown on create) — same meaning, different code, so both must be mapped.
  LICENCE_ALREADY_REGISTERED: 'A driver with this driving licence number is already registered.',
  DRIVER_NOT_FOUND: 'Driver could not be found in your fleet.',
  INVALID_DRIVER_STATUS_TRANSITION: 'Driver status can only be set to Active or Inactive — OnTrip is managed automatically during trips.',
  VEHICLE_NOT_FOUND: 'Vehicle could not be found in your fleet.',
  VEHICLE_REGISTRATION_ALREADY_EXISTS: 'A vehicle with this registration number already exists in your fleet.',
  VEHICLE_CANNOT_BE_MODIFIED: 'Vehicles on a trip or retired cannot be edited.',
  INVALID_VEHICLE_STATUS_TRANSITION: 'This vehicle status change is not allowed. OnTrip is managed during trips and Retired is final.',
}

/**
 * Returns a human-friendly error message for an agency or fleet error.
 * @param {object} [error]
 * @returns {string}
 */
export function getAgencyErrorMessage(error) {
  if (error?.code && AGENCY_ERROR_MESSAGES[error.code]) {
    return AGENCY_ERROR_MESSAGES[error.code]
  }

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
    if (message.includes('VehicleType') || message.includes('$.vehicleType')) {
      return 'Invalid vehicle type. Please select a supported vehicle type (Lorry, Container, Refrigerated, Flatbed, or Tipper).'
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
