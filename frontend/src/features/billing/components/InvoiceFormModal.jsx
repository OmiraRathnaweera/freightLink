import { useState } from 'react'
import { X, Loader2 } from 'lucide-react'
import { useTripsQuery, useTripDetailQuery } from '../../trips/api/tripsApi.js'
import { formatCurrency } from '../lib/formatters.js'

// This system has no direct customers and no manual price negotiation at invoicing time: every
// invoice bills the shipper who owns the trip's load, for the price already agreed when the job
// proposal / AI match was accepted (Assignment.ProposedPrice). So Create Invoice only ever needs a
// trip (to derive the shipper and default amount) plus an editable amount and optional notes — no
// recipient picker, no multi-line-item builder, no currency choice, no discount, no due date.
function InvoiceFormModal({ isOpen, onClose, onSubmit, initialInvoice = null, isSubmitting = false }) {
  const isEditing = Boolean(initialInvoice && (initialInvoice.id || initialInvoice.invoiceId))

  const [tripId, setTripId] = useState('')
  const [amount, setAmount] = useState('')
  const [notes, setNotes] = useState('')
  const [errors, setErrors] = useState({})
  const [hasEditedAmount, setHasEditedAmount] = useState(false)
  const [prefilledForTripId, setPrefilledForTripId] = useState(null)

  // Only Delivered trips without an invoice yet are eligible — a trip can be invoiced exactly once.
  const { data: tripsPage, isLoading: isLoadingTrips } = useTripsQuery(
    { status: 'Delivered', hasInvoice: false, pageSize: 100 },
    { enabled: isOpen && !isEditing },
  )
  const eligibleTrips = tripsPage?.items ?? []

  const { data: selectedTrip, isLoading: isLoadingTripDetail } = useTripDetailQuery(tripId, {
    enabled: isOpen && !isEditing && Boolean(tripId),
  })

  // Reset the form each time the modal opens for a (possibly new) invoice.
  const resetKey = isOpen ? (initialInvoice?.id || initialInvoice?.invoiceId || 'new') : null
  const [populatedForKey, setPopulatedForKey] = useState(resetKey)
  if (resetKey !== populatedForKey) {
    setPopulatedForKey(resetKey)
    if (resetKey !== null) {
      if (initialInvoice) {
        setTripId(initialInvoice.tripId || initialInvoice.linkedEntityId || '')
        setAmount(String(initialInvoice.totalAmount ?? initialInvoice.amount ?? ''))
        setNotes(initialInvoice.notes || '')
      } else {
        setTripId('')
        setAmount('')
        setNotes('')
      }
      setHasEditedAmount(false)
      setPrefilledForTripId(null)
      setErrors({})
    }
  }

  // Once the selected trip's detail resolves, default the amount to its agreed price — but only
  // until the user types their own value, so re-fetches don't clobber a manual edit. Adjusted
  // directly during render (same "derive state from a changed value" pattern as resetKey above)
  // rather than in an effect, per https://react.dev/learn/you-might-not-need-an-effect —
  // `prefilledForTripId` tracks the last trip this ran for, so it only fires once per trip change.
  if (!isEditing && selectedTrip && !hasEditedAmount && prefilledForTripId !== tripId) {
    setAmount(String(selectedTrip.agreedPrice ?? ''))
    setPrefilledForTripId(tripId)
  }

  if (!isOpen) return null

  const validate = () => {
    const errs = {}
    if (!isEditing && !tripId) {
      errs.tripId = 'Select the delivered trip to bill.'
    }
    const amountNum = Number(amount)
    if (!amount || Number.isNaN(amountNum) || amountNum <= 0) {
      errs.amount = 'Amount must be greater than zero.'
    }
    setErrors(errs)
    return Object.keys(errs).length === 0
  }

  const handleSubmit = (issueImmediately) => {
    if (!validate()) return

    const payload = isEditing
      ? {
          amount: Number(amount),
          notes: notes.trim() || null,
        }
      : {
          tripId,
          amount: Number(amount),
          notes: notes.trim() || null,
          issueImmediately,
        }

    onSubmit(payload, isEditing ? initialInvoice.id || initialInvoice.invoiceId : null)
  }

  const shipperName = isEditing
    ? initialInvoice.recipientName || 'Shipper'
    : selectedTrip?.shipperName || (tripId ? 'Loading…' : '—')
  const tripReference = isEditing
    ? initialInvoice.invoiceNumber
    : selectedTrip?.referenceCode || (tripId ? 'Loading…' : '—')

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-primary/40 backdrop-blur-xs overflow-y-auto animate-in fade-in duration-200">
      <div
        className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl ring-1 ring-slate-200 my-8 max-h-[90vh] flex flex-col"
        role="dialog"
        aria-modal="true"
      >
        {/* Header */}
        <div className="flex items-center justify-between pb-4 border-b border-slate-100 shrink-0">
          <div>
            <h2 className="text-lg font-bold text-slate-900">
              {isEditing ? `Edit Invoice (${initialInvoice.invoiceNumber || 'Draft'})` : 'Create Invoice'}
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              {isEditing
                ? 'Adjust the billed amount or notes before issuing.'
                : 'Bill the shipper for a delivered trip at the already-agreed price.'}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-500"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Form Body */}
        <div className="overflow-y-auto flex-1 py-4 pr-1 space-y-4">
          {!isEditing && (
            <div>
              <label htmlFor="trip-select" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-1.5">
                Delivered Trip
              </label>
              <select
                id="trip-select"
                value={tripId}
                onChange={(e) => {
                  setTripId(e.target.value)
                  setHasEditedAmount(false)
                }}
                disabled={isLoadingTrips}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-primary focus:ring-1 focus:ring-primary"
              >
                <option value="">-- Select a delivered trip --</option>
                {eligibleTrips.map((t) => (
                  <option key={t.tripId} value={t.tripId}>
                    {t.referenceCode || `Trip #${t.tripId.slice(0, 8)}`}
                  </option>
                ))}
              </select>
              {errors.tripId && <p className="text-[10px] text-rose-500 mt-0.5">{errors.tripId}</p>}
              {!isLoadingTrips && eligibleTrips.length === 0 && (
                <p className="text-[11px] text-slate-400 mt-1">
                  No delivered trips are awaiting an invoice right now.
                </p>
              )}
            </div>
          )}

          <div className="grid grid-cols-2 gap-4">
            <div>
              <span className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-1.5">
                {isEditing ? 'Invoice' : 'Trip Reference'}
              </span>
              <p className="text-sm font-medium text-slate-900 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
                {tripReference}
              </p>
            </div>
            <div>
              <span className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-1.5">
                Shipper
              </span>
              <p className="text-sm font-medium text-slate-900 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 truncate">
                {shipperName}
              </p>
            </div>
          </div>

          <div>
            <label htmlFor="amount-input" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-1.5">
              Amount (LKR)
            </label>
            <input
              id="amount-input"
              type="number"
              min="0.01"
              step="0.01"
              value={amount}
              onChange={(e) => {
                setAmount(e.target.value)
                setHasEditedAmount(true)
              }}
              placeholder={isLoadingTripDetail ? 'Loading agreed price…' : '0.00'}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-primary focus:ring-1 focus:ring-primary"
            />
            {!isEditing && selectedTrip?.agreedPrice != null && (
              <p className="text-[11px] text-slate-400 mt-1">
                Pre-filled from the agreed job price: {formatCurrency(selectedTrip.agreedPrice, true)}
              </p>
            )}
            {errors.amount && <p className="text-[10px] text-rose-500 mt-0.5">{errors.amount}</p>}
          </div>

          <div>
            <label htmlFor="notes-textarea" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-1.5">
              Notes (optional)
            </label>
            <textarea
              id="notes-textarea"
              rows={2}
              maxLength={2000}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="e.g. Standard 7-day payment term. Bank transfers to Commercial Bank A/C 10293847."
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 focus:border-primary focus:ring-1 focus:ring-primary"
            />
          </div>
        </div>

        {/* Footer Actions */}
        <div className="flex items-center justify-between pt-4 border-t border-slate-100 shrink-0">
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50"
          >
            Cancel
          </button>

          <div className="flex items-center gap-2">
            {isEditing ? (
              <button
                type="button"
                onClick={() => handleSubmit(false)}
                disabled={isSubmitting}
                className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-xs hover:bg-primary/90 focus:ring-2 focus:ring-primary focus:ring-offset-2"
              >
                {isSubmitting && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
                Save Changes
              </button>
            ) : (
              <>
                <button
                  type="button"
                  onClick={() => handleSubmit(false)}
                  disabled={isSubmitting}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-medium text-slate-800 hover:bg-slate-50 shadow-xs"
                >
                  {isSubmitting && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
                  Save Draft
                </button>
                <button
                  type="button"
                  onClick={() => handleSubmit(true)}
                  disabled={isSubmitting}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-xs hover:bg-primary/90 focus:ring-2 focus:ring-primary focus:ring-offset-2"
                >
                  {isSubmitting && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
                  Issue Immediately
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

export default InvoiceFormModal
