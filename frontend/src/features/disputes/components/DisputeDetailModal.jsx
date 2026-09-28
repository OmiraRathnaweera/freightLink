import { useEffect } from 'react'
import { X, AlertCircle, Truck, User, MapPin } from 'lucide-react'
import Button from '../../../components/Button.jsx'
import DisputeCategoryBadge from './DisputeCategoryBadge.jsx'
import DisputeStatusBadge from './DisputeStatusBadge.jsx'
import { canStartReview, canResolveDispute, isDisputeResolved } from '../lib/disputeRules.js'

export default function DisputeDetailModal({
  dispute,
  isOpen,
  onClose,
  onStartReview,
  onOpenResolve,
  onOpenResolution,
  isReviewPending,
}) {
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

  const formattedRaisedDate = new Date(dispute.raisedDate).toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })

  return (
    <div
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-primary/50 p-4 backdrop-blur-xs transition-opacity"
      role="dialog"
      aria-modal="true"
      aria-labelledby="dispute-detail-title"
    >
      <div className="relative w-full max-w-2xl rounded-xl border border-slate-border bg-surface-container-lowest shadow-2xl overflow-hidden max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-border bg-slate-50 px-6 py-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary text-white">
              <AlertCircle className="h-5 w-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 id="dispute-detail-title" className="text-headline-md text-on-surface">
                  Dispute #{dispute.displayId || dispute.disputeId?.slice(0, 8)}
                </h2>
                <DisputeStatusBadge status={dispute.status} />
              </div>
              <p className="text-xs text-on-surface-variant">
                Filed on <span className="font-mono font-medium">{formattedRaisedDate}</span>
              </p>
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

        {/* Scrollable Content */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6">
          {/* Category & Status Alert */}
          <div className="flex items-center justify-between p-4 rounded-lg bg-slate-50 border border-slate-200">
            <div>
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block">
                Dispute Classification
              </span>
              <div className="mt-1">
                <DisputeCategoryBadge category={dispute.category} />
              </div>
            </div>
            <div className="text-right">
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider block">
                State Machine Step
              </span>
              <span className="text-xs font-mono text-slate-700">
                {dispute.status === 'Raised' && 'Step 1/3 (Requires Review)'}
                {dispute.status === 'UnderReview' && 'Step 2/3 (Under Investigation)'}
                {dispute.status === 'Resolved' && 'Step 3/3 (Completed)'}
              </span>
            </div>
          </div>

          {/* Description Section */}
          <div className="space-y-2">
            <h3 className="text-sm font-bold uppercase tracking-wider text-slate-700">
              Dispute Statement & Details
            </h3>
            <div className="rounded-lg border border-slate-200 bg-white p-4 text-sm text-slate-800 leading-relaxed shadow-xs whitespace-pre-wrap">
              {dispute.description}
            </div>
          </div>

          {/* Two-Column Grid: Raised By & Linked Trip */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Raised By */}
            <div className="rounded-lg border border-slate-200 bg-slate-50/70 p-4 space-y-2.5">
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-600">
                <User className="h-4 w-4 text-primary" />
                Claimant Information
              </div>
              <div className="space-y-1">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-bold text-slate-900">{dispute.raisedByUser?.name}</span>
                  <span className="rounded bg-primary/10 text-primary px-2 py-0.5 text-xs font-semibold">
                    {dispute.raisedByUser?.role}
                  </span>
                </div>
                <p className="text-xs text-slate-600 font-medium">{dispute.raisedByUser?.company}</p>
                <div className="pt-2 text-xs space-y-1 text-slate-500 border-t border-slate-200">
                  <div>Email: <span className="font-mono text-slate-700">{dispute.raisedByUser?.email}</span></div>
                  <div>Phone: <span className="font-mono text-slate-700">{dispute.raisedByUser?.phone}</span></div>
                </div>
              </div>
            </div>

            {/* Linked Trip */}
            <div className="rounded-lg border border-slate-200 bg-slate-50/70 p-4 space-y-2.5">
              <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-600">
                <Truck className="h-4 w-4 text-primary" />
                Linked Trip Details
              </div>
              <div className="space-y-1">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-mono font-bold text-primary">#{dispute.trip?.tripId}</span>
                  <span className="text-xs font-mono text-slate-600 bg-slate-200/80 px-2 py-0.5 rounded">
                    {dispute.trip?.truckRegNo}
                  </span>
                </div>
                <div className="text-xs font-semibold text-slate-800 flex items-center gap-1.5 pt-1">
                  <MapPin className="h-3.5 w-3.5 text-primary" />
                  {dispute.trip?.routeSummary}
                </div>
                <div className="pt-2 text-xs space-y-1 text-slate-500 border-t border-slate-200">
                  <div>Origin: <span className="text-slate-700">{dispute.trip?.origin}</span></div>
                  <div>Dest: <span className="text-slate-700">{dispute.trip?.destination}</span></div>
                  <div>Carrier: <span className="text-slate-700">{dispute.trip?.carrierAgency}</span></div>
                </div>
              </div>
            </div>
          </div>

          {/* If Resolved, show preview card */}
          {dispute.status === 'Resolved' && dispute.resolution && (
            <div className="rounded-lg border border-emerald-200 bg-emerald-50/50 p-4 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold uppercase tracking-wider text-emerald-800">
                  Resolution Record Summary
                </span>
                <span className="text-xs font-bold text-emerald-900 bg-emerald-200 px-2 py-0.5 rounded">
                  Outcome: {dispute.resolution.outcome}
                </span>
              </div>
              <p className="text-xs text-slate-800 line-clamp-2 italic">
                "{dispute.resolution.notes}"
              </p>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="flex items-center justify-between border-t border-slate-border bg-slate-50 px-6 py-4">
          <Button variant="secondary" onClick={onClose}>
            Back to List
          </Button>

          <div className="flex items-center gap-2">
            {canStartReview(dispute.status) && (
              <Button
                variant="status"
                status="blue"
                onClick={() => {
                  onClose()
                  onStartReview(dispute.disputeId)
                }}
                disabled={isReviewPending}
              >
                Start Review
              </Button>
            )}

            {canResolveDispute(dispute.status) && (
              <Button
                variant="status"
                status="green"
                onClick={() => {
                  onClose()
                  onOpenResolve(dispute)
                }}
              >
                Resolve Dispute
              </Button>
            )}

            {isDisputeResolved(dispute.status) && (
              <Button
                variant="primary"
                onClick={() => {
                  onClose()
                  onOpenResolution(dispute)
                }}
              >
                View Full Resolution
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
