import { useState } from 'react'
import { AlertCircle, Loader2, X, XCircle } from 'lucide-react'
import Button from '../../../components/Button.jsx'
import Textarea from '../../../components/Textarea.jsx'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'
import { formatCurrency } from '../lib/format.js'

/**
 * Small confirmation modal for rejecting a proposal, with an optional reason the Agency will see.
 *
 * @param {object|null} proposal - the proposal being rejected, or null when the dialog is closed
 * @param {(reason: string) => void} onConfirm
 * @param {() => void} onClose
 * @param {boolean} isPending
 */
function RejectProposalDialog({ proposal, onConfirm, onClose, isPending }) {
  const [reason, setReason] = useState('')
  const isOpen = Boolean(proposal)

  useEscapeKey(isOpen, () => {
    if (!isPending) onClose()
  })

  if (!isOpen) return null

  function handleConfirm() {
    onConfirm(reason.trim())
  }

  return (
    <div
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-primary/50 p-4 backdrop-blur-xs"
      role="dialog"
      aria-modal="true"
      aria-labelledby="reject-proposal-title"
    >
      <div className="relative w-full max-w-md rounded-xl border border-slate-border bg-surface-container-lowest shadow-2xl">
        <div className="flex items-center justify-between border-b border-slate-border px-6 py-4">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-status-red-bg text-status-red-text">
              <XCircle className="h-5 w-5" />
            </div>
            <h2 id="reject-proposal-title" className="text-headline-md text-on-surface">
              Reject Proposal
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="rounded-lg p-1.5 text-on-surface-variant hover:bg-slate-200 disabled:opacity-50"
            aria-label="Close dialog"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="space-y-4 p-6">
          <div className="flex items-start gap-2 rounded-md border border-status-amber-text bg-status-amber-bg px-3.5 py-2.5 text-body-sm text-status-amber-text">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              Rejecting the {proposal?.agencyName ?? 'agency'}'s proposal of{' '}
              <strong>{formatCurrency(proposal?.proposedPrice)}</strong> on load{' '}
              {proposal?.loadReferenceCode ?? proposal?.loadId} cannot be undone.
            </span>
          </div>

          <div>
            <label htmlFor="reject-reason-input" className="mb-1.5 block text-sm font-semibold text-on-surface">
              Reason <span className="text-on-surface-variant font-normal">(optional, shown to the agency)</span>
            </label>
            <Textarea
              id="reject-reason-input"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={isPending}
              placeholder="e.g. Price too high for this load's budget."
            />
          </div>

          <div className="flex items-center justify-end gap-3 pt-2">
            <Button type="button" variant="secondary" onClick={onClose} disabled={isPending}>
              Cancel
            </Button>
            <Button type="button" variant="status" status="red" onClick={handleConfirm} disabled={isPending}>
              {isPending ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Rejecting...
                </>
              ) : (
                'Reject Proposal'
              )}
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}

export default RejectProposalDialog
