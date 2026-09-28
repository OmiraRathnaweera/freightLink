import { useMemo, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { AlertCircle, ChevronRight, RefreshCw, Scale } from 'lucide-react'

import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import { useDisputesQuery } from '../api/disputesApi.js'
import DisputeCategoryBadge from '../components/DisputeCategoryBadge.jsx'
import DisputeStatusBadge from '../components/DisputeStatusBadge.jsx'

const STATUS_FILTERS = [
  { value: 'All', label: 'All' },
  { value: 'Raised', label: 'Raised' },
  { value: 'UnderReview', label: 'Under Review' },
  { value: 'Resolved', label: 'Resolved' },
]

function formatDate(value) {
  return value
    ? new Date(value).toLocaleString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
    : '—'
}

export default function MyDisputesPage() {
  const role = useAppSelector((state) => state.auth?.role)
  const [status, setStatus] = useState('All')
  const { data: disputes = [], isLoading, isError, refetch, isFetching } = useDisputesQuery({ status })

  const counts = useMemo(
    () => Object.fromEntries(STATUS_FILTERS.map(({ value }) => [value, value === 'All' ? disputes.length : disputes.filter((item) => item.status === value).length])),
    [disputes],
  )

  if (role !== UserRole.SHIPPER && role !== UserRole.AGENCY_STAFF) {
    return <Navigate to="/unauthorized" replace />
  }

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Claims"
        title="Trip Disputes"
        description="Track disputes filed by you or the other party on trips that belong to your account. Admin decisions appear here after you refresh."
        actions={
          <Button variant="secondary" onClick={() => refetch()} disabled={isFetching}>
            <RefreshCw className={`mr-1.5 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} /> Refresh
          </Button>
        }
      />

      <Card className="p-0 overflow-hidden">
        <div className="flex flex-wrap gap-2 border-b border-slate-border bg-slate-50 p-4" role="tablist" aria-label="Dispute status">
          {STATUS_FILTERS.map((filter) => {
            const selected = filter.value === status
            return (
              <button
                key={filter.value}
                type="button"
                role="tab"
                aria-selected={selected}
                onClick={() => setStatus(filter.value)}
                className={`rounded-md px-3 py-1.5 text-xs font-bold transition-colors ${
                  selected ? 'bg-primary text-on-primary' : 'border border-slate-200 bg-white text-slate-700 hover:bg-slate-100'
                }`}
              >
                {filter.label} <span className="ml-1 font-mono">{counts[filter.value] ?? 0}</span>
              </button>
            )
          })}
        </div>

        {isLoading && <div className="p-12 text-center text-sm text-on-surface-variant">Loading trip disputes…</div>}
        {isError && (
          <div className="p-10 text-center">
            <p className="text-sm font-semibold text-status-red-text">Unable to load disputes.</p>
            <Button className="mt-3" variant="secondary" onClick={() => refetch()}>Try again</Button>
          </div>
        )}
        {!isLoading && !isError && disputes.length === 0 && (
          <EmptyState
            icon={Scale}
            title="No disputes found"
            description={status === 'All' ? 'Disputes raised for your trips will appear here.' : `No ${status} disputes are available.`}
          />
        )}
        {!isLoading && !isError && disputes.length > 0 && (
          <div className="divide-y divide-slate-100">
            {disputes.map((dispute) => (
              <Link
                key={dispute.disputeId}
                to={`/my-disputes/${dispute.disputeId}`}
                className="flex items-center gap-4 p-4 transition-colors hover:bg-slate-50"
              >
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <DisputeCategoryBadge category={dispute.category} />
                    <DisputeStatusBadge status={dispute.status} />
                  </div>
                  <p className="mt-2 truncate text-sm font-semibold text-on-surface">{dispute.trip?.routeSummary || `Trip ${dispute.tripId}`}</p>
                  <p className="mt-0.5 text-xs text-on-surface-variant">
                    Filed by {dispute.raisedByUser?.name || 'account holder'} · Raised {formatDate(dispute.createdAt)} · Updated {formatDate(dispute.updatedAt || dispute.raisedDate)}
                  </p>
                </div>
                <ChevronRight className="h-5 w-5 shrink-0 text-slate-400" />
              </Link>
            ))}
          </div>
        )}
      </Card>

      <p className="flex items-start gap-2 text-xs text-on-surface-variant">
        <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
        To file a new dispute, open the relevant invoice from Billing and select “Raise dispute”.
      </p>
    </div>
  )
}
