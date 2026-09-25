import { Calendar, ChevronDown, RotateCcw, Search, X } from 'lucide-react'
import { STATUS_FILTER_OPTIONS, DATE_RANGE_OPTIONS } from '../lib/invoiceStatus.js'

function InvoiceFilterBar({
  searchQuery,
  onSearchChange,
  statusFilter,
  onStatusChange,
  dateRange,
  onDateRangeChange,
  onResetFilters,
  hasActiveFilters,
}) {
  return (
    <div className="flex flex-col gap-3 rounded-lg border border-slate-border bg-surface-container-lowest p-3 shadow-soft sm:flex-row sm:items-center sm:justify-between">
      {/* Search Input */}
      <div className="relative flex-1 min-w-[240px]">
        <Search
          className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400"
          strokeWidth={1.75}
        />
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder="Search invoices, IDs, shippers..."
          className="h-10 w-full rounded-md border border-slate-300 bg-white pl-9 pr-9 text-body-md text-on-surface placeholder:text-slate-400 transition-colors focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
        />
        {searchQuery && (
          <button
            type="button"
            onClick={() => onSearchChange('')}
            aria-label="Clear search"
            className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      {/* Filter Selects & Actions */}
      <div className="flex flex-wrap items-center gap-2">
        {/* Status Dropdown */}
        <div className="relative">
          <select
            value={statusFilter}
            onChange={(e) => onStatusChange(e.target.value)}
            className="h-10 appearance-none rounded-md border border-slate-300 bg-white pl-3.5 pr-8 text-body-md font-medium text-on-surface transition-colors focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary cursor-pointer"
          >
            {STATUS_FILTER_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
          <ChevronDown
            className="pointer-events-none absolute right-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500"
            strokeWidth={1.75}
          />
        </div>

        {/* Date Range Dropdown */}
        <div className="relative">
          <div className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-500">
            <Calendar className="h-4 w-4" strokeWidth={1.75} />
          </div>
          <select
            value={dateRange}
            onChange={(e) => onDateRangeChange(e.target.value)}
            className="h-10 appearance-none rounded-md border border-slate-300 bg-white pl-9 pr-8 text-body-md font-medium text-on-surface transition-colors focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary cursor-pointer"
          >
            {DATE_RANGE_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
          <ChevronDown
            className="pointer-events-none absolute right-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500"
            strokeWidth={1.75}
          />
        </div>

        {/* More Filters / Reset Action */}
        {hasActiveFilters && (
          <button
            type="button"
            onClick={onResetFilters}
            className="inline-flex h-10 items-center gap-1.5 rounded-md border border-slate-300 bg-white px-3 text-body-md font-medium text-slate-600 hover:bg-slate-50 hover:text-on-surface transition-colors cursor-pointer"
            title="Reset filters"
          >
            <RotateCcw className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">Reset</span>
          </button>
        )}
      </div>
    </div>
  )
}

export default InvoiceFilterBar
