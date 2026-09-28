// User-friendly copy and mapping helpers for auth API error envelopes.
// Follows the same pattern as src/features/loads/lib/errorMessages.js.

const AUTH_ERROR_MESSAGES = {
  VALIDATION_ERROR: 'Please check the highlighted fields and try again.',
  EMAIL_ALREADY_EXISTS: 'An account with this email address already exists.',
  BUSINESS_REG_EXISTS: 'An agency with this business registration number already exists.',
  CONFLICT: 'An account or organization with these details already exists.',
  INVALID_CREDENTIALS: 'The email address or password you entered is incorrect.',
  USER_LOCKED_OUT: 'This account has been locked. Please contact support.',
  EMAIL_ALREADY_REGISTERED: 'An account with this email address already exists.',
  INCORRECT_CURRENT_PASSWORD: 'The current password you entered is incorrect.',
}

/**
 * Returns a human-friendly error message for an auth error.
 * @param {object} [error]
 * @returns {string}
 */
export function getAuthErrorMessage(error) {
  if (error?.status === 409) {
    return AUTH_ERROR_MESSAGES.CONFLICT
  }
  return AUTH_ERROR_MESSAGES[error?.code] ?? error?.message ?? 'Something went wrong. Please check your inputs and try again.'
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
      // Handles both camelCase and PascalCase backend field names
      const normalizedField = detail.field.charAt(0).toLowerCase() + detail.field.slice(1)
      errors[normalizedField] = detail.issue
    }
    return errors
  }, {})
}
