import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Eye, MoreVertical, Pencil } from 'lucide-react'
import { useClickOutside } from '../../../hooks/useClickOutside.js'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'

// My Loads dashboard row actions — a feature component since the menu
// items (View/Edit) and their routes are specific to the Loads domain
// (.claude/rules/frontend-design.md #1). The triple-dot button opens a
// small popup menu rather than acting as a disguised single Edit link.
function RowActionsMenu({ loadId }) {
  const [isOpen, setIsOpen] = useState(false)
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
          <Link
            to={`/loads/${loadId}/edit`}
            role="menuitem"
            onClick={close}
            className="flex items-center gap-2 px-3 py-2 text-body-md text-on-surface hover:bg-slate-100"
          >
            <Pencil className="h-4 w-4" strokeWidth={1.5} />
            Edit
          </Link>
        </div>
      )}
    </div>
  )
}

export default RowActionsMenu
