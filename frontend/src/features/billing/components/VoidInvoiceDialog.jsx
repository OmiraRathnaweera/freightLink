import { useState } from 'react'
import { AlertTriangle, Loader2, X } from 'lucide-react'

function VoidInvoiceDialog({ isOpen, onClose, onConfirm, invoice, isSubmitting }) {
  const [voidReason, setVoidReason] = useState('')
  const [error, setError] = useState('')

  if (!isOpen || !invoice) return null

  const handleSubmit = (e) => {
    e.preventDefault()
    if (!voidReason.trim()) {
      setError('Please provide a specific reason for voiding this invoice.')
      return
    }
    setError('')
    onConfirm(invoice.id || invoice.invoiceId, voidReason.trim())
  }

  const handleClose = () => {
    if (isSubmitting) return
    setVoidReason('')
    setError('')
    onClose()
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-200">
      <div
        className="w-full max-w-md rounded-xl bg-white p-6 shadow-2xl ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-800"
        role="dialog"
        aria-modal="true"
        aria-labelledby="void-dialog-title"
      >
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-rose-100 text-rose-600 dark:bg-rose-950/60 dark:text-rose-400">
              <AlertTriangle className="h-5 w-5" />
            </div>
            <div>
              <h3 id="void-dialog-title" className="text-base font-semibold text-slate-900 dark:text-white">
                Void Invoice
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {invoice.invoiceNumber || 'Selected Invoice'}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleClose}
            disabled={isSubmitting}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-500 dark:hover:bg-slate-800 dark:hover:text-slate-300"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <p className="text-sm text-slate-600 dark:text-slate-300">
            Voiding will cancel this invoice permanently and transition its status to{' '}
            <span className="font-semibold text-rose-600 dark:text-rose-400">Voided</span>. A reason must be recorded for audit compliance.
          </p>

          <div>
            <label htmlFor="voidReason" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300">
              Reason for Voiding <span className="text-rose-500">*</span>
            </label>
            <textarea
              id="voidReason"
              name="voidReason"
              rows={3}
              value={voidReason}
              onChange={(e) => {
                setVoidReason(e.target.value)
                if (error) setError('')
              }}
              placeholder="e.g. Rate dispute resolved with trip discount, double billing, or customer cancelled order"
              className="mt-1.5 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-rose-500 focus:ring-1 focus:ring-rose-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              autoFocus
            />
            {error && <p className="mt-1 text-xs font-medium text-rose-600 dark:text-rose-400">{error}</p>}
          </div>

          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={handleClose}
              disabled={isSubmitting}
              className="rounded-lg border border-slate-300 px-4 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting || !voidReason.trim()}
              className="inline-flex items-center gap-2 rounded-lg bg-rose-600 px-4 py-2 text-xs font-medium text-white hover:bg-rose-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Voiding...
                </>
              ) : (
                'Confirm Void'
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default VoidInvoiceDialog
