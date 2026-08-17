import { History } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { useLoadStatusHistoryQuery } from '../api/loadsApi.js'
import { getLoadStatusTone } from '../lib/statusTone.js'
import { formatDateTime } from '../lib/format.js'

// Status transition timeline for a load — GET /loads/{id}/status-history
// (see loadsApi.js's getLoadStatusHistory comment: this endpoint isn't
// implemented on the backend yet, so it 404s today). Built against the
// future contract now rather than left out or faked: the query's isError
// branch renders a graceful "not available yet" placeholder instead of a
// scary ErrorState, and this card starts rendering real data automatically
// the moment the backend ships the route — no further frontend changes.
function LoadStatusHistoryCard({ loadId }) {
  const historyQuery = useLoadStatusHistoryQuery(loadId)

  return (
    <Card>
      <h3 className="mb-4 text-headline-md text-primary">Status History</h3>

      {historyQuery.isLoading ? (
        <p className="text-body-md text-on-surface-variant">Loading history…</p>
      ) : historyQuery.isError ? (
        <div className="flex flex-col items-center gap-2 py-8 text-center">
          <History className="h-8 w-8 text-on-surface-variant" strokeWidth={1.5} />
          <p className="text-body-md text-on-surface-variant">Status history isn't available yet.</p>
          <p className="text-body-md text-on-surface-variant">It'll appear here automatically once this is enabled on the backend.</p>
        </div>
      ) : historyQuery.data.length > 0 ? (
        <ol className="space-y-4">
          {[...historyQuery.data]
            .sort((a, b) => new Date(a.changedAt) - new Date(b.changedAt))
            .map((entry, index, entries) => (
              <li key={entry.loadStatusHistoryId} className="relative pl-6">
                {index < entries.length - 1 && (
                  <span className="absolute left-[5px] top-4 h-full w-px bg-slate-border" aria-hidden="true" />
                )}
                <span className="absolute left-0 top-1.5 h-2.5 w-2.5 rounded-full bg-primary" aria-hidden="true" />
                <div className="flex flex-wrap items-center gap-2">
                  <StatusBadge tone={getLoadStatusTone(entry.toStatus)}>{entry.toStatus}</StatusBadge>
                  {entry.fromStatus && <span className="text-body-md text-on-surface-variant">from {entry.fromStatus}</span>}
                </div>
                <p className="mt-1 text-body-md text-on-surface-variant">{formatDateTime(entry.changedAt)}</p>
                {entry.reason && <p className="mt-1 text-body-md text-on-surface">{entry.reason}</p>}
              </li>
            ))}
        </ol>
      ) : (
        <p className="text-body-md text-on-surface-variant">No status changes recorded yet.</p>
      )}
    </Card>
  )
}

export default LoadStatusHistoryCard
