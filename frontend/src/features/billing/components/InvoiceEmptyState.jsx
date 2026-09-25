import { FileText, RotateCcw } from 'lucide-react'

function InvoiceEmptyState({ onClearFilters, isFiltered }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4 text-center">
      {/* Icon Graphic */}
      <div className="relative mb-5 flex h-20 w-20 items-center justify-center rounded-2xl bg-slate-100 text-slate-400">
        <FileText className="h-10 w-10 stroke-[1.25]" />
      </div>

      <h3 className="font-heading text-lg font-bold text-on-surface">No invoices found</h3>
      <p className="mt-1.5 max-w-sm text-body-md text-slate-500">
        {isFiltered
          ? 'There are currently no invoices matching your search or active filters. Try adjusting your criteria.'
          : 'No invoices have been issued in the system yet.'}
      </p>

      {isFiltered && (
        <button
          type="button"
          onClick={onClearFilters}
          className="mt-6 inline-flex items-center gap-2 rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold uppercase tracking-wider text-slate-700 shadow-xs hover:bg-slate-50 transition-colors cursor-pointer"
        >
          <RotateCcw className="h-3.5 w-3.5" />
          Clear All Filters
        </button>
      )}
    </div>
  )
}

export default InvoiceEmptyState
