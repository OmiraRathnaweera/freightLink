import { MapPin, History } from "lucide-react";
import Card from "../../../components/Card.jsx";
import StatusBadge from "../../../components/StatusBadge.jsx";
import { getTripStatusTone } from "../lib/statusTone.js";
import { formatDateTime } from "../lib/format.js";

/**
 * Status/Timeline monitor component for a Trip.
 * Renders the chronological audit trail of status transitions with timestamps,
 * transition notes, and location snapshots.
 */
function TripTimelineCard({ events = [] }) {
  const sortedEvents = [...events].sort(
    (a, b) => new Date(b.occurredAt) - new Date(a.occurredAt)
  );

  return (
    <Card>
      <div className="flex items-center gap-2 mb-4">
        <History className="h-5 w-5 text-primary" strokeWidth={1.5} />
        <h3 className="text-headline-md text-primary">Status & Timeline Monitor</h3>
      </div>

      {sortedEvents.length > 0 ? (
        <ol className="space-y-6">
          {sortedEvents.map((event, index) => {
            const hasNext = index < sortedEvents.length - 1;
            return (
              <li key={event.tripEventId} className="relative pl-6">
                {hasNext && (
                  <span
                    className="absolute left-[7px] top-4 h-full w-px bg-slate-200"
                    aria-hidden="true"
                  />
                )}
                <span
                  className="absolute left-0 top-1.5 h-3.5 w-3.5 rounded-full border-2 border-white bg-primary shadow-sm"
                  aria-hidden="true"
                />

                <div className="flex flex-wrap items-center gap-2">
                  {event.fromStatus && (
                    <>
                      <StatusBadge tone={getTripStatusTone(event.fromStatus)}>
                        {event.fromStatus}
                      </StatusBadge>
                      <span className="text-on-surface-variant">→</span>
                    </>
                  )}
                  <StatusBadge tone={getTripStatusTone(event.toStatus)}>
                    {event.toStatus}
                  </StatusBadge>
                </div>

                <p className="mt-1.5 text-xs text-on-surface-variant font-mono">
                  {formatDateTime(event.occurredAt)}
                </p>

                {event.notes && (
                  <p className="mt-1 text-body-md text-on-surface bg-slate-50 rounded-md p-2 border border-slate-100">
                    {event.notes}
                  </p>
                )}

                {event.snapshotLat != null && event.snapshotLng != null && (
                  <div className="mt-1.5 inline-flex items-center gap-1 text-xs text-on-surface-variant bg-slate-100 rounded px-2 py-0.5">
                    <MapPin className="h-3.5 w-3.5 text-slate-500" strokeWidth={1.5} />
                    <span>
                      GPS: {Number(event.snapshotLat).toFixed(4)}, {Number(event.snapshotLng).toFixed(4)}
                    </span>
                  </div>
                )}
              </li>
            );
          })}
        </ol>
      ) : (
        <p className="text-body-md text-on-surface-variant">
          No status transitions recorded yet.
        </p>
      )}
    </Card>
  );
}

export default TripTimelineCard;
