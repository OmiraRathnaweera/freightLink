import { LoadStatus, UserRole } from '../../../lib/enums.js'

// Single source of truth for role- and status-driven load-mutation
// visibility — consumed by RowActionsMenu, LoadsPage, LoadDetailPage,
// LoadFilesSection, and EditLoadPage's own guard, so none of them can
// drift out of sync on who may do what.
//
// Every Load mutation endpoint (create/update/cancel/attach-file/
// detach-file) is Shipper (owner) only per the backend contract — Admin's
// Load access is read-only (GET /loads, GET /loads/{id}) even though
// Admin can view any shipper's load, regardless of its status. These
// helpers are what the UI checks before rendering a mutation control; the
// backend remains the actual enforcement layer regardless of what the UI
// hides (a stale/cached role or status could still get a 403/422).
const EDITABLE_STATUSES = [LoadStatus.DRAFT, LoadStatus.POSTED]
const CANCELLABLE_STATUSES = [LoadStatus.DRAFT, LoadStatus.POSTED, LoadStatus.MATCHED]

export function isLoadEditable(status) {
  return EDITABLE_STATUSES.includes(status)
}

export function isLoadCancellable(status) {
  return CANCELLABLE_STATUSES.includes(status)
}

export function canCreateLoad(role) {
  return role === UserRole.SHIPPER
}

export function canEditLoad(role, status) {
  return role === UserRole.SHIPPER && isLoadEditable(status)
}

export function canCancelLoad(role, status) {
  return role === UserRole.SHIPPER && isLoadCancellable(status)
}

export function canManageLoadFiles(role) {
  return role === UserRole.SHIPPER
}
