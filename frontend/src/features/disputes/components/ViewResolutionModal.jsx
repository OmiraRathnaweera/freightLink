import { useEffect } from 'react'
import { X, CheckCircle, Calendar, UserCheck, ShieldCheck, FileText, ArrowRight } from 'lucide-react'
import Button from '../../../components/Button.jsx'
import DisputeCategoryBadge from './DisputeCategoryBadge.jsx'
import DisputeStatusBadge from './DisputeStatusBadge.jsx'

export default function ViewResolutionModal({ dispute, isOpen, onClose }) {
  // Handle Escape key
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === 'Escape' && isOpen) {
        onClose()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isOpen, onClose])

  if (!isOpen || !dispute) return null

  const resolution = dispute.resolution || {}
  const resolvedDate = resolution.resolvedAt
    ? new Date(resolution.resolvedAt).toLocaleString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      })
    : 'N/A'

  return (
    <div
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-primary/50 p-4 backdrop-blur-xs transition-opacity"
      role="dialog"
      aria-modal="true"
      aria-labelledby="view-resolution-modal-title"
    >
      <div className="relative w-full max-w-xl rounded-xl border border-slate-border bg-surface-container-lowest shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-border bg-emerald-50/70 px-6 py-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-emerald-600 text-white shadow-xs">
              <ShieldCheck className="h-5 w-5" strokeWidth={2} />
            </div>
            <div>
              <h2 id="view-resolution-modal-title" className="text-headline-md text-emerald-950">
                Dispute Resolution Record
              </h2>
              <div className="flex items-center gap-2 mt-0.5">
                <span className="font-mono text-xs font-bold text-emerald-900">
                  #{dispute.displayId || dispute.disputeId?.slice(0, 8)}
                </span>
                <span className="text-xs text-emerald-700">• Read-Only Audit Log</span>
              </div>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-200 transition-colors"
            aria-label="Close modal"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-5">
          {/* Status & Outcome Banner */}
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-200 bg-slate-50 p-3.5">
            <div>
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block">
                Current Lifecycle Status
              </span>
              <div className="mt-1">
                <DisputeStatusBadge status={dispute.status} />
              </div>
            </div>
            <div>
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block">
                Formal Outcome
              </span>
              <span className="mt-1 inline-flex items-center rounded-md bg-emerald-100 px-2.5 py-0.5 text-xs font-bold text-emerald-800 border border-emerald-300">
                {resolution.outcome || 'Upheld'}
              </span>
            </div>
            <div>
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block">
                Category
              </span>
              <div className="mt-1">
                <DisputeCategoryBadge category={dispute.category} />
              </div>
            </div>
          </div>

          {/* Reference Details */}
          <div className="grid grid-cols-2 gap-3 text-xs">
            <div className="rounded border border-slate-200 p-3 bg-white">
              <span className="font-semibold text-slate-500 uppercase block mb-1">Raised By</span>
              <div className="font-bold text-slate-900 text-sm">{dispute.raisedByUser?.name}</div>
              <div className="text-slate-600">{dispute.raisedByUser?.company}</div>
              <div className="text-slate-500 font-mono mt-0.5">{dispute.raisedByUser?.email}</div>
            </div>
            <div className="rounded border border-slate-200 p-3 bg-white">
              <span className="font-semibold text-slate-500 uppercase block mb-1">Linked Trip</span>
              <div className="font-mono font-bold text-primary text-sm">#{dispute.trip?.tripId}</div>
              <div className="text-slate-700 font-medium">{dispute.trip?.routeSummary}</div>
              <div className="text-slate-500 mt-0.5">Carrier: {dispute.trip?.carrierAgency}</div>
            </div>
          </div>

          {/* Original Complaint */}
          <div>
            <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block mb-1.5">
              Original Dispute Details
            </span>
            <p className="text-xs text-slate-700 bg-slate-50 p-3 rounded-lg border border-slate-200 leading-relaxed">
              {dispute.description}
            </p>
          </div>

          {/* Mandatory Resolution Note Section */}
          <div className="rounded-lg border-2 border-emerald-200 bg-emerald-50/50 p-4 space-y-2">
            <div className="flex items-center gap-2 text-emerald-900 font-bold text-sm">
              <FileText className="h-4 w-4 text-emerald-700" />
              Administrative Resolution Note
            </div>
            <p className="text-sm text-slate-900 bg-white p-3.5 rounded-md border border-emerald-200 leading-relaxed font-sans whitespace-pre-wrap shadow-xs">
              {resolution.notes || 'No resolution notes provided.'}
            </p>

            <div className="pt-2 flex flex-wrap items-center justify-between text-xs text-slate-600 gap-2 border-t border-emerald-100">
              <div className="flex items-center gap-1.5">
                <Calendar className="h-3.5 w-3.5 text-emerald-700" />
                <span>Resolved: </span>
                <span className="font-mono font-semibold text-slate-800">{resolvedDate}</span>
              </div>
              <div className="flex items-center gap-1.5">
                <UserCheck className="h-3.5 w-3.5 text-emerald-700" />
                <span>Resolved by: </span>
                <span className="font-semibold text-slate-800">
                  {resolution.resolvedByUser?.name || 'Authorized Admin'}
                </span>
              </div>
            </div>
          </div>

          {/* Footer */}
          <div className="flex items-center justify-end pt-2 border-t border-slate-border">
            <Button variant="primary" onClick={onClose}>
              Close Record
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}
