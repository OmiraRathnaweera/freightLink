import { useState } from "react";
import { AlertTriangle, X } from "lucide-react";
import Button from "../../../components/Button.jsx";
import { useCancelTripMutation } from "../api/tripsApi.js";
import { getTripErrorMessage } from "../lib/errorMessages.js";

function CancelTripDialog({ tripId, onClose }) {
  const [reason, setReason] = useState("");

  const cancelMutation = useCancelTripMutation({
    onSuccess: () => {
      onClose();
    },
  });

  function handleSubmit(e) {
    e.preventDefault();
    cancelMutation.mutate({
      tripId,
      data: { reason: reason.trim() || undefined },
    });
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
    >
      <div className="relative w-full max-w-md rounded-xl bg-surface p-6 shadow-xl border border-slate-200">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-2 text-red-600 font-semibold text-title-md">
            <AlertTriangle className="h-5 w-5" />
            <span>Cancel Trip</span>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-on-surface-variant hover:text-on-surface p-1 rounded-md"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <p className="text-body-md text-on-surface-variant">
            Are you sure you want to cancel this trip? In accordance with platform governance (ADR-019), the trip will be moved to Cancelled status with an audit record.
          </p>

          <div>
            <label className="block text-label-caps text-on-surface-variant mb-1">
              Cancellation Reason (Optional)
            </label>
            <textarea
              rows={3}
              className="w-full rounded-md border border-slate-300 bg-surface px-3 py-2 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
              placeholder="e.g. Shipper requested cancellation or vehicle breakdown"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={500}
            />
          </div>

          {cancelMutation.isError && (
            <div className="rounded-md bg-red-50 p-3 text-xs text-red-700 border border-red-200">
              {getTripErrorMessage(cancelMutation.error)}
            </div>
          )}

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <Button type="button" variant="secondary" onClick={onClose}>
              Close
            </Button>
            <Button
              type="submit"
              variant="destructive"
              disabled={cancelMutation.isPending}
            >
              {cancelMutation.isPending ? "Cancelling..." : "Confirm Cancellation"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default CancelTripDialog;
