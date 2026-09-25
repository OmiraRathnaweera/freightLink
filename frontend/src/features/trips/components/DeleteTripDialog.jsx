import { AlertTriangle, Trash2, Truck, User, X } from "lucide-react";
import { toast } from "sonner";
import Button from "../../../components/Button.jsx";
import StatusBadge from "../../../components/StatusBadge.jsx";
import { useDeleteTripMutation } from "../api/tripsApi.js";
import { getTripErrorMessage } from "../lib/errorMessages.js";
import { formatDriverName, formatTripId } from "../lib/format.js";
import { getTripStatusTone } from "../lib/statusTone.js";

/**
 * Dialog for deleting / cancelling an active dispatched trip.
 * Adheres to platform governance (ADR-019 soft-delete) by transitioning
 * the trip to Cancelled status and freeing assigned vehicle/driver fleet resources.
 *
 * @param {object} props
 * @param {string} props.tripId
 * @param {object} [props.trip]
 * @param {() => void} props.onClose
 * @param {() => void} [props.onDeleted]
 */
function DeleteTripDialog({ tripId, trip, onClose, onDeleted }) {
  const deleteMutation = useDeleteTripMutation({
    onSuccess: () => {
      toast.success("Trip permanently deleted");
      onDeleted?.();
      onClose();
    },
  });

  function handleSubmit(e) {
    e.preventDefault();
    deleteMutation.mutate({ tripId });
  }

  const formattedTripId = formatTripId(tripId);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="delete-trip-title"
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
    >
      <div className="relative w-full max-w-md rounded-xl bg-surface p-6 shadow-xl border border-slate-200 animate-in fade-in zoom-in-95 duration-150">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-2.5 text-red-600 font-semibold text-title-md">
            <div className="p-1.5 rounded-lg bg-red-50 text-red-600 border border-red-100">
              <Trash2 className="h-5 w-5" />
            </div>
            <span id="delete-trip-title">Delete Trip</span>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-on-surface-variant hover:text-on-surface p-1 rounded-md transition-colors"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <p className="text-body-md text-on-surface-variant">
            Are you sure you want to permanently delete trip <strong className="text-on-surface font-mono">{formattedTripId}</strong>?
            This will completely remove the trip and its records from the system. If you no longer want this trip, deleting it will fully clear it and allow the underlying assignment to be managed or re-dispatched.
          </p>

          <div className="rounded-md bg-amber-50 p-3 text-xs text-amber-800 border border-amber-200 flex items-start gap-2">
            <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5 text-amber-600" />
            <span><strong>Warning:</strong> This action is permanent and cannot be undone.</span>
          </div>

          {/* Trip Summary Card */}
          {trip && (
            <div className="rounded-lg border border-slate-200 bg-slate-50/75 p-3 space-y-2 text-xs">
              <div className="flex items-center justify-between">
                <span className="font-semibold text-slate-700">Trip Identifier:</span>
                <span className="font-mono font-medium text-slate-900">{formattedTripId}</span>
              </div>
              {trip.status && (
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-slate-700">Status:</span>
                  <StatusBadge tone={getTripStatusTone(trip.status)}>
                    {trip.status}
                  </StatusBadge>
                </div>
              )}
              {(trip.driverName || trip.driverId) && (
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-slate-700 inline-flex items-center gap-1">
                    <User className="h-3.5 w-3.5 text-slate-500" />
                    Driver:
                  </span>
                  <span className="text-slate-800">{formatDriverName(trip.driverName, trip.driverId)}</span>
                </div>
              )}
              {(trip.vehicleRegistrationNo || trip.vehicleId) && (
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-slate-700 inline-flex items-center gap-1">
                    <Truck className="h-3.5 w-3.5 text-slate-500" />
                    Vehicle:
                  </span>
                  <span className="text-slate-800 font-medium">{trip.vehicleRegistrationNo || trip.vehicleId?.substring(0, 8)}</span>
                </div>
              )}
            </div>
          )}

          {deleteMutation.isError && (
            <div className="rounded-md bg-red-50 p-3 text-xs text-red-700 border border-red-200 flex items-start gap-2">
              <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5 text-red-600" />
              <span>{getTripErrorMessage(deleteMutation.error)}</span>
            </div>
          )}

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <Button type="button" variant="secondary" onClick={onClose} disabled={deleteMutation.isPending}>
              Keep Trip
            </Button>
            <Button
              type="submit"
              variant="destructive"
              disabled={deleteMutation.isPending}
              className="inline-flex items-center gap-1.5"
            >
              <Trash2 className="h-4 w-4" />
              {deleteMutation.isPending ? "Deleting..." : "Permanently Delete Trip"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default DeleteTripDialog;
