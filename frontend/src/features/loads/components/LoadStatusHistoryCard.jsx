import Card from '../../../components/Card.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { getLoadStatusTone } from '../lib/statusTone.js'
import { formatDateTime } from '../lib/format.js'

// Status transition timeline for a load. There is no separate
// status-history endpoint — GET /loads/{id} embeds the full timeline as
// `statusHistory` on the LoadResponseDto itself (newest first), so this is
// a plain presentational component fed straight from the already-fetched
// load, not its own query.
function LoadStatusHistoryCard({ history }) {
  return (
    <Card>
      <h3 className="mb-4 text-headline-md text-primary">Status History</h3>

      {history.length > 0 ? (
        <ol className="space-y-4">
          {[...history]
            // .sort((a, b) => new Date(a.changedAt) - new Date(b.changedAt))
            .map((entry, index, entries) => (
              <li key={entry.loadStatusHistoryId} className="relative pl-6">
                {index < entries.length - 1 && (
                  <span className="absolute left-[5px] top-4 h-full w-px bg-slate-border" aria-hidden="true" />
                )}
                <span className="absolute left-0 top-1.5 h-2.5 w-2.5 rounded-full bg-primary" aria-hidden="true" />
                <div className="flex flex-wrap items-center gap-2">
                  <StatusBadge tone={getLoadStatusTone(entry.toStatus)}>{entry.toStatus}</StatusBadge>
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
