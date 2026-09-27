import { Link } from "react-router-dom";
import { ArrowDown, ArrowUp, ArrowUpDown, ChevronRight, Trash2 } from "lucide-react";
import StatusBadge from "../../../components/StatusBadge.jsx";
import { cx } from "../../../lib/cx.js";
import { useAppSelector } from "../../../hooks/useAppSelector.js";
import { TripStatus, UserRole } from "../../../lib/enums.js";
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

function TripsTable({ trips, sortBy, sortDir, onSortChange, onDeleteTrip }) {
  const role = useAppSelector((state) => state?.auth?.role);

  const canDeleteTripRole = role === UserRole.AGENCY_STAFF;

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
          {trips.map((trip, index) => {
            const canDeleteThisTrip =
              canDeleteTripRole &&
              trip.status !== TripStatus.DELIVERED &&
              trip.status !== "Delivered" &&
              (trip.status === TripStatus.CANCELLED ||
                trip.status === "Cancelled" ||
                trip.status === TripStatus.ASSIGNED ||
                trip.status === "Assigned");

            return (
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
                  <div className="flex items-center justify-end gap-2.5">
                    <Link
                      to={`/trips/${trip.tripId}`}
                      className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:text-primary/80 transition-colors"
                    >
                      View
                      <ChevronRight className="h-4 w-4" />
                    </Link>
                    {canDeleteThisTrip && onDeleteTrip && (
                      <button
                        type="button"
                        onClick={() => onDeleteTrip(trip)}
                        className="inline-flex items-center gap-1 text-xs font-medium text-red-600 hover:text-red-700 hover:bg-red-50 px-2 py-1 rounded transition-colors"
                        title={`Delete trip ${formatTripId(trip.tripId)}`}
                        aria-label={`Delete trip ${formatTripId(trip.tripId)}`}
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                        <span>Delete</span>
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export default TripsTable;
