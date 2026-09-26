import { useState, useEffect } from 'react'
import { X, CheckCircle2, AlertCircle, Loader2, ArrowRight } from 'lucide-react'
import Button from '../../../components/Button.jsx'
import Textarea from '../../../components/Textarea.jsx'
import DisputeCategoryBadge from './DisputeCategoryBadge.jsx'
import { OUTCOME_OPTIONS, validateResolutionPayload } from '../lib/disputeRules.js'

export default function ResolutionModal({ dispute, isOpen, onClose, onConfirm, isPending }) {
  const [outcome, setOutcome] = useState('Upheld')
  const [resolutionNote, setResolutionNote] = useState('')
  const [error, setError] = useState(null)
  const [touched, setTouched] = useState(false)

  // Reset form when modal opens with a new dispute
  useEffect(() => {
    if (isOpen) {
      setOutcome('Upheld')
      setResolutionNote('')
      setError(null)
      setTouched(false)
    }
  }, [isOpen, dispute?.disputeId])

  // Handle Escape key
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === 'Escape' && isOpen && !isPending) {
        onClose()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isOpen, isPending, onClose])

  if (!isOpen || !dispute) return null

  const handleNoteChange = (e) => {
    const val = e.target.value
    setResolutionNote(val)
    if (touched) {
      setError(validateResolutionPayload(val))
    }
  }

  const handleSubmit = (e) => {
    e.preventDefault()
    setTouched(true)
    const validationError = validateResolutionPayload(resolutionNote)
    if (validationError) {
      setError(validationError)
      return
    }

    onConfirm({
      disputeId: dispute.disputeId,
      outcome,
      resolutionNote: resolutionNote.trim(),
    })
  }

  return (
    <div
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-primary/50 p-4 backdrop-blur-xs transition-opacity"
      role="dialog"
      aria-modal="true"
      aria-labelledby="resolution-modal-title"
    >
      <div className="relative w-full max-w-xl rounded-xl border border-slate-border bg-surface-container-lowest shadow-2xl overflow-hidden">
        {/* Modal Header */}
        <div className="flex items-center justify-between border-b border-slate-border bg-slate-50/80 px-6 py-4">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-emerald-100 text-emerald-700">
              <CheckCircle2 className="h-5 w-5" strokeWidth={2} />
            </div>
            <div>
              <h2 id="resolution-modal-title" className="text-headline-md text-on-surface">
                Resolve Dispute
              </h2>
              <p className="text-xs font-mono text-on-surface-variant">
                Adjudication for #{dispute.displayId || dispute.disputeId?.slice(0, 8)}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="rounded-lg p-1.5 text-on-surface-variant hover:bg-slate-200 transition-colors disabled:opacity-50"
            aria-label="Close modal"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Modal Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-5">
          {/* Dispute Context Summary Card */}
          <div className="rounded-lg border border-slate-200 bg-slate-50/70 p-4 text-sm space-y-3">
            <div className="grid grid-cols-2 gap-3 pb-3 border-b border-slate-200">
              <div>
                <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block">
                  Dispute Reference
                </span>
                <span className="font-mono font-bold text-slate-900 text-sm">
                  #{dispute.displayId || dispute.disputeId?.slice(0, 8)}
                </span>
              </div>
              <div>
                <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block">
                  Linked Trip
                </span>
                <span className="font-mono font-bold text-primary text-sm flex items-center gap-1">
                  #{dispute.trip?.tripId}
                  <span className="font-sans text-xs font-normal text-slate-600 truncate">
                    ({dispute.trip?.routeSummary})
                  </span>
                </span>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3 pb-3 border-b border-slate-200">
              <div>
                <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block">
                  Raised By
                </span>
                <div className="font-semibold text-slate-900 flex items-center gap-1.5">
                  {dispute.raisedByUser?.name}
                  <span className="rounded bg-slate-200 px-1.5 py-0.5 text-[11px] font-medium text-slate-700">
                    {dispute.raisedByUser?.role}
                  </span>
                </div>
                <div className="text-xs text-slate-500 truncate">{dispute.raisedByUser?.email}</div>
              </div>
              <div>
                <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block">
                  Category
                </span>
                <div className="mt-0.5">
                  <DisputeCategoryBadge category={dispute.category} />
                </div>
              </div>
            </div>

            <div>
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-1">
                Original Reason / Complaint
              </span>
              <p className="text-xs text-slate-700 bg-white p-2.5 rounded border border-slate-200 leading-relaxed italic">
                "{dispute.description}"
              </p>
            </div>
          </div>

          {/* Adjudication Outcome Field */}
          <div>
            <label className="block text-sm font-semibold text-on-surface mb-1.5">
              Adjudication Outcome <span className="text-status-red-text">*</span>
            </label>
            <div className="grid grid-cols-3 gap-2">
              {OUTCOME_OPTIONS.map((opt) => (
                <button
                  type="button"
                  key={opt.value}
                  onClick={() => setOutcome(opt.value)}
                  className={`px-3 py-2 text-xs font-semibold rounded-lg border text-center transition-all ${
                    outcome === opt.value
                      ? 'border-primary bg-primary text-white shadow-xs'
                      : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-100'
                  }`}
                >
                  {opt.label.split(' ')[0]}
                  <span className="block text-[10px] opacity-80 font-normal">
                    {opt.label.includes('(') ? opt.label.slice(opt.label.indexOf('(')) : ''}
                  </span>
                </button>
              ))}
            </div>
          </div>

          {/* Resolution Note Field */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label htmlFor="resolution-note-input" className="block text-sm font-semibold text-on-surface">
                Resolution Note <span className="text-status-red-text">*</span>
              </label>
              <span className="text-xs text-on-surface-variant font-mono">
                {resolutionNote.trim().length} chars (mandatory)
              </span>
            </div>
            <Textarea
              id="resolution-note-input"
              rows={4}
              value={resolutionNote}
              onChange={handleNoteChange}
              onBlur={() => {
                setTouched(true)
                setError(validateResolutionPayload(resolutionNote))
              }}
              error={Boolean(error)}
              disabled={isPending}
              placeholder="Provide a comprehensive explanation of the administrative investigation, compensation or penalty agreement, and final resolution instructions..."
            />
            {error && (
              <p className="mt-1.5 flex items-center gap-1.5 text-xs text-status-red-text font-medium">
                <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                {error}
              </p>
            )}
            <p className="mt-1 text-xs text-on-surface-variant">
              Per ticket Y3S01-81 rules, this resolution note is permanently locked and visible to the Shipper and Agency once resolved.
            </p>
          </div>

          {/* Action Footer */}
          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-border">
            <Button
              type="button"
              variant="secondary"
              onClick={onClose}
              disabled={isPending}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              variant="status"
              status="green"
              disabled={isPending || resolutionNote.trim().length === 0}
            >
              {isPending ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin mr-1" />
                  Confirming Resolution...
                </>
              ) : (
                <>
                  <CheckCircle2 className="h-4 w-4 mr-1" />
                  Confirm Resolution
                </>
              )}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
