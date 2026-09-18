import { useState } from "react";
import { AlertCircle, Loader2, MapPin, X } from "lucide-react";
import Button from "../../../components/Button.jsx";
import Input from "../../../components/Input.jsx";
import { TripStatus } from "../../../lib/enums.js";
import { useChangeTripStatusMutation } from "../api/tripsApi.js";
import { getTripErrorMessage } from "../lib/errorMessages.js";

function getNextStatusOptions(currentStatus) {
  switch (currentStatus) {
    case TripStatus.ASSIGNED:
    case "Assigned":
      return [
        { value: TripStatus.PICKED_UP, label: "Picked Up (Requires Pickup Proof)" },
        { value: TripStatus.CANCELLED, label: "Cancelled" },
      ];
    case TripStatus.PICKED_UP:
    case "PickedUp":
      return [
        { value: TripStatus.IN_TRANSIT, label: "In Transit" },
        { value: TripStatus.CANCELLED, label: "Cancelled" },
      ];
    case TripStatus.IN_TRANSIT:
    case "InTransit":
      return [
        { value: TripStatus.DELIVERED, label: "Delivered (Requires Delivery Proof)" },
        { value: TripStatus.CANCELLED, label: "Cancelled" },
      ];
    default:
      return [];
  }
}

function ChangeTripStatusDialog({ tripId, currentStatus, evidence = [], onClose }) {
  const options = getNextStatusOptions(currentStatus);
  const [targetStatus, setTargetStatus] = useState(options[0]?.value ?? "");
  const [notes, setNotes] = useState("");
  const [snapshotLat, setSnapshotLat] = useState("");
  const [snapshotLng, setSnapshotLng] = useState("");
  const [isLocating, setIsLocating] = useState(false);
  const [geoError, setGeoError] = useState(null);

  const changeStatusMutation = useChangeTripStatusMutation({
    onSuccess: () => {
      onClose();
    },
  });

  function handleGetLocation() {
    if (!navigator.geolocation) {
      setGeoError("Geolocation is not supported by your browser.");
      return;
    }
    setIsLocating(true);
    setGeoError(null);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setSnapshotLat(pos.coords.latitude.toFixed(6));
        setSnapshotLng(pos.coords.longitude.toFixed(6));
        setIsLocating(false);
      },
      (err) => {
        setGeoError(err.message || "Failed to retrieve location.");
        setIsLocating(false);
      },
      { timeout: 10000, enableHighAccuracy: true }
    );
  }

  const hasPickupProof = evidence.some(
    (e) => e.evidenceType === "PickupProof" || e.evidenceType === "pickupProof"
  );
  const hasDeliveryProof = evidence.some(
    (e) => e.evidenceType === "DeliveryProof" || e.evidenceType === "deliveryProof"
  );

  const requiresMissingEvidence =
    (targetStatus === TripStatus.PICKED_UP && !hasPickupProof) ||
    (targetStatus === TripStatus.DELIVERED && !hasDeliveryProof);

  function handleSubmit(e) {
    e.preventDefault();
    if (!targetStatus) return;

    changeStatusMutation.mutate({
      tripId,
      data: {
        targetStatus,
        notes: notes.trim() || undefined,
        snapshotLat: snapshotLat ? Number(snapshotLat) : undefined,
        snapshotLng: snapshotLng ? Number(snapshotLng) : undefined,
      },
    });
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
    >
      <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <h3 className="text-headline-md text-on-surface">Update Trip Status</h3>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <div>
            <label className="block text-sm font-medium text-on-surface mb-1">
              Next Status
            </label>
            <select
              value={targetStatus}
              onChange={(e) => setTargetStatus(e.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border"
            >
              {options.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          {requiresMissingEvidence && (
            <div className="flex items-start gap-2 rounded-md bg-amber-50 p-3 text-xs text-amber-800 border border-amber-200">
              <AlertCircle className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" />
              <span>
                {targetStatus === TripStatus.PICKED_UP
                  ? "Proof of Pickup is required before moving to Picked Up. Please upload pickup evidence first."
                  : "Proof of Delivery is required before moving to Delivered. Please upload delivery evidence first."}
              </span>
            </div>
          )}

          <div>
            <label className="block text-sm font-medium text-on-surface mb-1">
              Transition Notes (Optional)
            </label>
            <textarea
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="e.g. Loading completed at warehouse gate 3"
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border"
            />
          </div>

          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="text-sm font-medium text-on-surface">
                Location Snapshot (Optional)
              </label>
              <button
                type="button"
                onClick={handleGetLocation}
                disabled={isLocating}
                className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
              >
                {isLocating ? (
                  <Loader2 className="h-3 w-3 animate-spin" />
                ) : (
                  <MapPin className="h-3 w-3" />
                )}
                <span>{isLocating ? "Locating..." : "Use Current Location"}</span>
              </button>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <Input
                type="number"
                step="any"
                placeholder="Latitude"
                value={snapshotLat}
                onChange={(e) => setSnapshotLat(e.target.value)}
              />
              <Input
                type="number"
                step="any"
                placeholder="Longitude"
                value={snapshotLng}
                onChange={(e) => setSnapshotLng(e.target.value)}
              />
            </div>
            {geoError && <p className="mt-1 text-xs text-red-600">{geoError}</p>}
          </div>

          {changeStatusMutation.isError && (
            <div className="rounded-md bg-red-50 p-3 text-xs text-red-700 border border-red-200">
              {getTripErrorMessage(changeStatusMutation.error)}
            </div>
          )}

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <Button type="button" variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={changeStatusMutation.isPending || !targetStatus || requiresMissingEvidence}
            >
              {changeStatusMutation.isPending ? "Updating..." : "Update Status"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default ChangeTripStatusDialog;
