// User-friendly copy for every Load/Load-file error code in
// docs/load-management-api.md Section 1. Codes not listed here (an
// unmapped backend addition, a network failure) fall back to
// axiosClient's own normalized `error.message`.
const LOAD_ERROR_MESSAGES = {
  LOAD_NOT_FOUND: 'This load no longer exists.',
  LOAD_NOT_OWNED: "You don't have access to this load.",
  INVALID_PICKUP_WINDOW: 'Pickup window end must be after the start.',
  LOAD_PICKUP_DROPOFF_IDENTICAL: 'Pickup and dropoff locations cannot be identical.',
  INVALID_LOAD_STATUS_TRANSITION: 'This load can no longer be changed in its current status.',
  LOAD_CANCEL_REASON_REQUIRED: 'A cancellation reason is required.',
  LOAD_PAGE_OUT_OF_RANGE: 'That page is out of range.',
  LOAD_REFERENCE_CODE_CONFLICT: 'Could not generate a unique reference code — please try again.',
  LOAD_CONCURRENCY_CONFLICT: 'This load was updated elsewhere since you opened it. Reload and try again.',
  VALIDATION_ERROR: 'Please fix the highlighted fields.',
  LOAD_FILE_UPLOAD_NOT_FOUND: 'Upload not found — try uploading the file again.',
  LOAD_FILE_NOT_FOUND: 'That file attachment no longer exists.',
  FILE_NOT_OWNED: "You don't have access to this file.",
  FILE_IN_USE: 'This file is already attached to a load.',
}

export function getLoadErrorMessage(error) {
  return LOAD_ERROR_MESSAGES[error?.code] ?? error?.message ?? 'Something went wrong. Please try again.'
}

// Turns a VALIDATION_ERROR's `details: [{ field, issue }]` (the error
// envelope's Section 1 shape) into a Formik `setErrors`-shaped object.
export function mapValidationDetailsToFormik(details) {
  if (!Array.isArray(details)) return {}
  return details.reduce((errors, detail) => {
    if (detail?.field) errors[detail.field] = detail.issue
    return errors
  }, {})
}
