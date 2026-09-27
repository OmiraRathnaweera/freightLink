import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Check, FileSignature, X } from 'lucide-react'
import { toast } from 'sonner'
import Card from '../../../components/Card.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import Button from '../../../components/Button.jsx'
import { LoadProposalStatus } from '../../../lib/enums.js'
import {
  useAllLoadProposalsQuery,
  useAcceptAnyLoadProposalMutation,
  useRejectAnyLoadProposalMutation,
} from '../api/loadProposalsApi.js'
import { getLoadProposalStatusTone } from '../lib/proposalStatusTone.js'
import { formatCurrency, formatDateTime } from '../lib/format.js'
import RejectProposalDialog from '../components/RejectProposalDialog.jsx'

const STATUS_FILTERS = ['ALL', ...Object.values(LoadProposalStatus)]

/**
 * Shipper-only aggregate view of every price proposal agencies have sent across all of the
 * shipper's loads — the counterpart to the per-load proposal list that SendProposalDialog
 * (agency side) creates into, but which nothing previously surfaced back to the Shipper.
 */
export default function LoadProposalsPage() {
  const { data: proposals = [], isLoading, isError, refetch } = useAllLoadProposalsQuery()
  const [statusFilter, setStatusFilter] = useState('ALL')
  const [rejectingProposal, setRejectingProposal] = useState(null)

  const acceptMutation = useAcceptAnyLoadProposalMutation({
    onSuccess: () => toast.success('Proposal accepted — load matched with this agency.'),
    onError: (error) => toast.error(error?.message ?? 'Failed to accept proposal.'),
  })

  const rejectMutation = useRejectAnyLoadProposalMutation({
    onSuccess: () => {
      toast.success('Proposal rejected.')
      setRejectingProposal(null)
    },
    onError: (error) => toast.error(error?.message ?? 'Failed to reject proposal.'),
  })

  const filteredProposals = useMemo(() => {
    if (statusFilter === 'ALL') return proposals
    return proposals.filter((p) => p.status === statusFilter)
  }, [proposals, statusFilter])

  function handleAccept(proposal) {
    acceptMutation.mutate({ loadId: proposal.loadId, proposalId: proposal.loadProposalId })
  }

  function handleRejectConfirm(reason) {
    rejectMutation.mutate({
      loadId: rejectingProposal.loadId,
      proposalId: rejectingProposal.loadProposalId,
      reason,
    })
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Load Proposals"
        description="Price proposals agencies have sent directly on your posted loads."
      />

      <Card className="p-4">
        <div className="flex flex-wrap items-center gap-2">
          {STATUS_FILTERS.map((status) => (
            <button
              key={status}
              type="button"
              onClick={() => setStatusFilter(status)}
              className={`rounded-md border px-3 py-1.5 text-xs font-semibold transition-colors ${
                statusFilter === status
                  ? 'border-primary bg-primary text-on-primary'
                  : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-100'
              }`}
            >
              {status === 'ALL' ? 'All' : status}
            </button>
          ))}
        </div>
      </Card>

      <Card className="overflow-hidden p-0">
        {isLoading ? (
          <div className="space-y-3 p-6">
            {[...Array(4)].map((_, i) => (
              <Skeleton key={i} className="h-14 w-full" />
            ))}
          </div>
        ) : isError ? (
          <ErrorState description="Could not load your proposals. Please try again." onRetry={refetch} />
        ) : filteredProposals.length === 0 ? (
          <EmptyState
            icon={FileSignature}
            title={proposals.length === 0 ? 'No proposals yet' : 'No matching proposals'}
            description={
              proposals.length === 0
                ? 'When an agency sends a price proposal on one of your posted loads, it will show up here.'
                : 'Try a different status filter.'
            }
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-border bg-slate-50 text-body-xs font-semibold uppercase text-slate-600">
                <tr>
                  <th className="px-6 py-3.5">Load</th>
                  <th className="px-6 py-3.5">Agency</th>
                  <th className="px-6 py-3.5">Proposed Price</th>
                  <th className="px-6 py-3.5">Message</th>
                  <th className="px-6 py-3.5">Status</th>
                  <th className="px-6 py-3.5">Submitted</th>
                  <th className="px-6 py-3.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredProposals.map((proposal) => {
                  const isPending = proposal.status === LoadProposalStatus.PENDING
                  const isActing =
                    (acceptMutation.isPending &&
                      acceptMutation.variables?.proposalId === proposal.loadProposalId) ||
                    (rejectMutation.isPending &&
                      rejectMutation.variables?.proposalId === proposal.loadProposalId)
                  return (
                    <tr key={proposal.loadProposalId} className="transition-colors hover:bg-slate-50/50">
                      <td className="px-6 py-4">
                        <Link
                          to={`/loads/${proposal.loadId}`}
                          className="font-mono font-semibold text-primary hover:underline"
                        >
                          {proposal.loadReferenceCode ?? proposal.loadId.slice(0, 8)}
                        </Link>
                      </td>
                      <td className="px-6 py-4 text-slate-700">{proposal.agencyName ?? '—'}</td>
                      <td className="px-6 py-4 font-mono font-semibold text-slate-900">
                        {formatCurrency(proposal.proposedPrice)}
                      </td>
                      <td className="max-w-xs truncate px-6 py-4 text-slate-600" title={proposal.message ?? ''}>
                        {proposal.message ?? '—'}
                      </td>
                      <td className="px-6 py-4">
                        <StatusBadge tone={getLoadProposalStatusTone(proposal.status)}>
                          {proposal.status}
                        </StatusBadge>
                      </td>
                      <td className="px-6 py-4 text-slate-600">{formatDateTime(proposal.createdAt)}</td>
                      <td className="px-6 py-4 text-right">
                        {isPending ? (
                          <div className="inline-flex items-center gap-2">
                            <Button
                              type="button"
                              variant="status"
                              status="green"
                              disabled={isActing}
                              onClick={() => handleAccept(proposal)}
                              className="text-xs"
                            >
                              <Check className="h-3.5 w-3.5" />
                              Accept
                            </Button>
                            <Button
                              type="button"
                              variant="status"
                              status="red"
                              disabled={isActing}
                              onClick={() => setRejectingProposal(proposal)}
                              className="text-xs"
                            >
                              <X className="h-3.5 w-3.5" />
                              Reject
                            </Button>
                          </div>
                        ) : (
                          <span className="text-xs text-on-surface-variant">
                            {proposal.respondedAt ? formatDateTime(proposal.respondedAt) : '—'}
                          </span>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <RejectProposalDialog
        proposal={rejectingProposal}
        onConfirm={handleRejectConfirm}
        onClose={() => setRejectingProposal(null)}
        isPending={rejectMutation.isPending}
      />
    </div>
  )
}
