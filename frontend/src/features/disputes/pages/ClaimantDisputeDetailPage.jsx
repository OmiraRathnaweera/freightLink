import { Link, Navigate, useParams } from 'react-router-dom'
import { ArrowLeft, Calendar, CheckCircle2, FileText, RefreshCw, Route } from 'lucide-react'

import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import { useDisputeDetailQuery } from '../api/disputesApi.js'
import DisputeCategoryBadge from '../components/DisputeCategoryBadge.jsx'
import DisputeStatusBadge from '../components/DisputeStatusBadge.jsx'

function formatDate(value) {
  return value
    ? new Date(value).toLocaleString('en-US', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
    : '—'
}

export default function ClaimantDisputeDetailPage() {
  const role = useAppSelector((state) => state.auth?.role)
  const { disputeId } = useParams()
  const query = useDisputeDetailQuery(disputeId)

  if (role !== UserRole.SHIPPER && role !== UserRole.AGENCY_STAFF) {
    return <Navigate to="/unauthorized" replace />
  }

  if (query.isLoading) return <div className="p-12 text-center text-sm text-on-surface-variant">Loading dispute…</div>
  if (query.isError) return <ErrorState title="Unable to load dispute" description={query.error?.message || 'Please try again.'} onRetry={query.refetch} />
  const dispute = query.data
  if (!dispute) return null
  const resolution = dispute.resolution

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        eyebrow="Claims"
        title={`Trip Dispute #${dispute.displayId || dispute.disputeId.slice(0, 8)}`}
        description="This record is read-only after it has been submitted. Refresh to see the latest Admin decision."
        actions={
          <div className="flex gap-2">
            <Button variant="secondary" onClick={() => query.refetch()} disabled={query.isFetching}><RefreshCw className={`mr-1.5 h-4 w-4 ${query.isFetching ? 'animate-spin' : ''}`} />Refresh</Button>
            <Link to="/my-disputes"><Button variant="secondary"><ArrowLeft className="mr-1.5 h-4 w-4" />All disputes</Button></Link>
          </div>
        }
      />

      <Card className="space-y-5">
        <div className="flex flex-wrap items-center gap-2">
          <DisputeCategoryBadge category={dispute.category} />
          <DisputeStatusBadge status={dispute.status} />
        </div>
        <div>
          <p className="text-label-caps text-on-surface-variant">Dispute statement</p>
          <p className="mt-2 whitespace-pre-wrap rounded-lg border border-slate-200 bg-slate-50 p-4 text-sm leading-relaxed text-on-surface">{dispute.description}</p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="rounded-lg border border-slate-200 p-4">
            <p className="flex items-center gap-1.5 text-label-caps text-on-surface-variant"><Route className="h-3.5 w-3.5" />Linked trip</p>
            <p className="mt-2 font-mono text-sm font-semibold text-primary">{dispute.trip?.tripId || dispute.tripId}</p>
            <p className="mt-1 text-sm text-on-surface">{dispute.trip?.routeSummary || 'Route details unavailable'}</p>
            {dispute.trip?.carrierAgency && <p className="mt-1 text-xs text-on-surface-variant">Carrier: {dispute.trip.carrierAgency}</p>}
          </div>
          <div className="rounded-lg border border-slate-200 p-4">
            <p className="flex items-center gap-1.5 text-label-caps text-on-surface-variant"><Calendar className="h-3.5 w-3.5" />Record history</p>
            <p className="mt-2 text-sm text-on-surface">Filed by {dispute.raisedByUser?.name || dispute.raisedByUserId}</p>
            <p className="mt-1 text-xs text-on-surface-variant">Raised {formatDate(dispute.createdAt || dispute.raisedDate)}</p>
            <p className="mt-1 text-xs text-on-surface-variant">Last updated {formatDate(dispute.updatedAt)}</p>
          </div>
        </div>
      </Card>

      {dispute.status === 'Resolved' && resolution && (
        <Card className="border-emerald-200 bg-emerald-50/40">
          <div className="flex items-center gap-2 text-emerald-900">
            <CheckCircle2 className="h-5 w-5" />
            <h2 className="text-headline-md">Admin Resolution</h2>
          </div>
          <div className="mt-4 rounded-lg border border-emerald-200 bg-white p-4">
            <p className="text-label-caps text-emerald-800">Outcome</p>
            <p className="mt-1 text-sm font-bold text-emerald-950">{resolution.outcome}</p>
            <p className="mt-4 flex items-center gap-1.5 text-label-caps text-emerald-800"><FileText className="h-3.5 w-3.5" />Resolution note</p>
            <p className="mt-1 whitespace-pre-wrap text-sm leading-relaxed text-on-surface">{resolution.notes}</p>
            <p className="mt-4 text-xs text-on-surface-variant">Resolved {formatDate(resolution.resolvedAt)}</p>
          </div>
        </Card>
      )}
    </div>
  )
}
