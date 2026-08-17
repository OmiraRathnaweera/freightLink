import { LoadStatus } from '../../../lib/enums.js'

// Single source of truth for status-driven row-action visibility
// (docs/load-management-api.md Section 2) — consumed by RowActionsMenu,
// LoadDetailPage's action buttons, and EditLoadPage's own guard, so all
// three never drift out of sync on which statuses allow which action.
const EDITABLE_STATUSES = [LoadStatus.DRAFT, LoadStatus.POSTED]
const CANCELLABLE_STATUSES = [LoadStatus.DRAFT, LoadStatus.POSTED, LoadStatus.MATCHED]

export function isLoadEditable(status) {
  return EDITABLE_STATUSES.includes(status)
}

export function isLoadCancellable(status) {
  return CANCELLABLE_STATUSES.includes(status)
}
