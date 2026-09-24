import { Link } from "react-router-dom";
import { ArrowDown, ArrowUp, ArrowUpDown, ChevronRight } from "lucide-react";
import StatusBadge from "../../../components/StatusBadge.jsx";
import { cx } from "../../../lib/cx.js";
import { getTripStatusTone } from "../lib/statusTone.js";
import {
  formatAgencyName,
  formatDateTime,
  formatDriverName,
  formatTripId,
} from "../lib/format.js";

function SortableHeader({ column, label, sortBy, sortDir, onSortChange }) {
  if (!onSortChange) {
    return <th className="py-table-cell-py pr-table-cell-px font-normal">{label}</th>;
  }
  const active = sortBy === column;
  const Icon = active ? (sortDir === "asc" ? ArrowUp : ArrowDown) : ArrowUpDown;

  return (
    <th className="py-table-cell-py pr-table-cell-px font-normal">
      <button
        type="button"
        onClick={() => onSortChange(column)}
        className={cx(
          "inline-flex items-center gap-1 text-label-caps transition-colors",
          active ? "text-on-surface" : "text-on-surface-variant hover:text-on-surface"
        )}
      >
        {label}
        <Icon className="h-3.5 w-3.5" strokeWidth={1.5} />
      </button>
    </th>
  );
}

function TripsTable({ trips, sortBy, sortDir, onSortChange }) {
  return (
    <div className="overflow-x-auto min-h-[60vh]">
      <table className="w-full text-left text-body-md">
        <thead>
          <tr className="text-label-caps text-on-surface-variant border-b border-slate-border">
            <th className="py-table-cell-py pl-4 pr-table-cell-px font-normal">
              Trip
            </th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">
              Status
            </th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">
              Agency
            </th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">
              Driver
            </th>
            <SortableHeader
              column="createdAt"
              label="Created"
              sortBy={sortBy}
              sortDir={sortDir}
              onSortChange={onSortChange}
            />
            <th className="py-table-cell-py pr-4 text-right font-normal">
              Action
            </th>
          </tr>
        </thead>
        <tbody>
          {trips.map((trip, index) => (
            <tr
              key={trip.tripId}
              className={cx(
                "group border-b border-slate-100 transition-colors hover:bg-slate-50",
                index % 2 === 1 && "bg-slate-50/50"
              )}
            >
              <td className="py-table-cell-py pl-4 pr-table-cell-px text-data-mono">
                <Link
                  to={`/trips/${trip.tripId}`}
                  className="font-medium text-primary hover:underline"
                >
                  {formatTripId(trip.tripId)}
                </Link>
              </td>
              <td className="py-table-cell-py pr-table-cell-px">
                <StatusBadge tone={getTripStatusTone(trip.status)}>
                  {trip.status}
                </StatusBadge>
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface">
                {formatAgencyName(trip.agencyName, trip.agencyId)}
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface">
                {formatDriverName(trip.driverName, trip.driverId)}
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface-variant">
                {formatDateTime(trip.createdAt)}
              </td>
              <td className="py-table-cell-py pr-4 text-right">
                <Link
                  to={`/trips/${trip.tripId}`}
                  className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:text-primary/80"
                >
                  View
                  <ChevronRight className="h-4 w-4" />
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default TripsTable;
