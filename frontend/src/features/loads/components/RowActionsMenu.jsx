import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Ban, Eye, MoreVertical, Pencil } from 'lucide-react'
import { useClickOutside } from '../../../hooks/useClickOutside.js'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'
import { canCancelLoad, canEditLoad } from '../lib/loadPermissions.js'
import CancelLoadDialog from './CancelLoadDialog.jsx'

// Load Control row actions — a feature component since the menu items
// (View/Edit/Cancel), their routes, and their role/status-gating are
// specific to the Loads domain (.claude/rules/frontend-design.md #1).
// Edit/Cancel are only rendered when both the caller's role and the
// load's current status allow them (canEditLoad/canCancelLoad —
// loadPermissions.js), not just disabled — Load mutations are Shipper
// (owner) only, and a stale/cached role or status could still get a
// 403/422 from the backend regardless of what the UI shows.
function RowActionsMenu({ loadId, status, role }) {
  const [isOpen, setIsOpen] = useState(false)
  const [isCancelOpen, setIsCancelOpen] = useState(false)
  const containerRef = useRef(null)

  const close = () => setIsOpen(false)
  useClickOutside(containerRef, isOpen, close)
  useEscapeKey(isOpen, close)

  return (
    <div ref={containerRef} className="relative inline-block text-left">
      <button
        type="button"
        onClick={() => setIsOpen((prev) => !prev)}
        aria-label={`Actions for ${loadId}`}
        aria-haspopup="menu"
        aria-expanded={isOpen}
        className="inline-flex h-8 w-8 items-center justify-center rounded text-on-surface-variant transition-colors hover:bg-slate-100 hover:text-on-surface"
      >
        <MoreVertical className="h-4 w-4" strokeWidth={1.5} />
      </button>
      {isOpen && (
        <div
          role="menu"
          className="absolute right-0 z-10 mt-1 w-36 rounded-md border border-slate-border bg-surface-container-lowest py-1 shadow-soft"
        >
          <Link
            to={`/loads/${loadId}`}
            role="menuitem"
            onClick={close}
            className="flex items-center gap-2 px-3 py-2 text-body-md text-on-surface hover:bg-slate-100"
          >
            <Eye className="h-4 w-4" strokeWidth={1.5} />
            View
          </Link>
          {canEditLoad(role, status) && (
            <Link
              to={`/loads/${loadId}/edit`}
              role="menuitem"
              onClick={close}
              className="flex items-center gap-2 px-3 py-2 text-body-md text-on-surface hover:bg-slate-100"
            >
              <Pencil className="h-4 w-4" strokeWidth={1.5} />
              Edit
            </Link>
          )}
          {canCancelLoad(role, status) && (
            <button
              type="button"
              role="menuitem"
              onClick={() => {
                close()
                setIsCancelOpen(true)
              }}
              className="flex w-full items-center gap-2 px-3 py-2 text-left text-body-md text-status-red-text hover:bg-slate-100"
            >
              <Ban className="h-4 w-4" strokeWidth={1.5} />
              Cancel
            </button>
          )}
        </div>
      )}
      {isCancelOpen && <CancelLoadDialog loadId={loadId} onClose={() => setIsCancelOpen(false)} />}
    </div>
  )
}

export default RowActionsMenu
