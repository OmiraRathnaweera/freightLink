import { ChevronLeft, ChevronRight } from 'lucide-react'
import Button from '../../../components/Button.jsx'
import { cx } from '../../../lib/cx.js'

const PAGE_SIZE_OPTIONS = [10, 20, 50]

// Windows the page-number strip down to first/last + a couple pages either
// side of the current one, so a 40-page result set doesn't render 40 buttons.
function getPageNumbers(page, totalPages) {
  const pages = new Set([1, totalPages, page - 1, page, page + 1])
  return [...pages].filter((value) => value >= 1 && value <= totalPages).sort((a, b) => a - b)
}

// Page controls for the Load Control table, bound to the list envelope's
// page/pageSize/totalItems/totalPages (docs/load-management-api.md
// Section 3.3) rather than a client-computed page count. Always renders
// the row-count + page-size controls; the prev/next/page-number strip only
// appears once there's more than one page.
function Pagination({ page, pageSize, totalItems, totalPages, onPageChange, onPageSizeChange }) {
  const start = totalItems === 0 ? 0 : (page - 1) * pageSize + 1
  const end = Math.min(page * pageSize, totalItems)
  const pageNumbers = getPageNumbers(page, totalPages)

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-border px-4 py-3">
      <div className="flex flex-wrap items-center gap-4">
        <p className="text-body-md text-on-surface-variant">
          Showing {start}–{end} of {totalItems}
        </p>
        <label className="flex items-center gap-1.5 text-body-md text-on-surface-variant">
          Rows per page
          <select
            value={pageSize}
            onChange={(event) => onPageSizeChange(Number(event.target.value))}
            className="rounded-md border border-slate-300 bg-white px-2 py-1 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border"
          >
            {PAGE_SIZE_OPTIONS.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center gap-1">
          <Button variant="secondary" disabled={page <= 1} onClick={() => onPageChange(page - 1)} aria-label="Previous page">
            <ChevronLeft className="h-4 w-4" strokeWidth={1.5} />
          </Button>

          {pageNumbers.map((pageNumber, index) => {
            const previousNumber = pageNumbers[index - 1]
            const showEllipsis = previousNumber != null && pageNumber - previousNumber > 1
            return (
              <span key={pageNumber} className="flex items-center">
                {showEllipsis && <span className="px-1 text-body-md text-on-surface-variant">…</span>}
                <button
                  type="button"
                  onClick={() => onPageChange(pageNumber)}
                  aria-current={pageNumber === page ? 'page' : undefined}
                  className={cx(
                    'flex h-8 min-w-8 items-center justify-center rounded-md px-2 text-body-md',
                    pageNumber === page
                      ? 'bg-primary text-on-primary'
                      : 'text-on-surface-variant hover:bg-slate-100 hover:text-on-surface',
                  )}
                >
                  {pageNumber}
                </button>
              </span>
            )
          })}

          <Button variant="secondary" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)} aria-label="Next page">
            <ChevronRight className="h-4 w-4" strokeWidth={1.5} />
          </Button>
        </div>
      )}
    </div>
  )
}

export default Pagination
