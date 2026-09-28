import { useState, useMemo } from "react";
import { AlertCircle, Edit2, SlidersHorizontal, Truck, User, X } from "lucide-react";
import Button from "../../../components/Button.jsx";
import Input from "../../../components/Input.jsx";
import { useUpdateTripMutation } from "../api/tripsApi.js";
import { useAgencyFleetQuery } from "../../agencies/api/agencyApi.js";
import { getTripErrorMessage } from "../lib/errorMessages.js";

function EditTripDialog({ trip, onClose }) {
  const [vehicleId, setVehicleId] = useState(trip.vehicleId || "");
  const [driverId, setDriverId] = useState(trip.driverId || "");
  const [notes, setNotes] = useState("");
  const [isManualMode, setIsManualMode] = useState(false);

  // Fetch agency fleet (vehicles & drivers)
  const fleetQuery = useAgencyFleetQuery(trip.agencyId, { staleTime: 60000 });
  const vehicles = useMemo(() => fleetQuery.data?.vehicles ?? [], [fleetQuery.data?.vehicles]);
  const drivers = useMemo(() => fleetQuery.data?.drivers ?? [], [fleetQuery.data?.drivers]);

  const selectedVehicle = useMemo(
    () => vehicles.find((v) => v.vehicleId === vehicleId),
    [vehicles, vehicleId]
  );

  const selectedDriver = useMemo(
    () => drivers.find((d) => d.driverId === driverId),
    [drivers, driverId]
  );

  const updateMutation = useUpdateTripMutation({
    onSuccess: () => {
      onClose();
    },
  });

  function handleSubmit(e) {
    e.preventDefault();
    updateMutation.mutate({
      tripId: trip.tripId,
      data: {
        vehicleId: vehicleId.trim() || undefined,
        driverId: driverId.trim() || undefined,
        notes: notes.trim() || undefined,
      },
    });
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs overflow-y-auto"
    >
      <div className="relative w-full max-w-lg rounded-2xl bg-surface p-6 shadow-2xl border border-slate-200 my-8">
        <div className="flex items-center justify-between pb-4 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Edit2 className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-title-md font-bold text-on-surface">Edit Trip Assignments</h2>
              <p className="text-body-sm text-on-surface-variant">
                Reassign vehicle or driver for this trip before departure.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-on-surface-variant hover:text-on-surface p-1.5 rounded-lg hover:bg-slate-100 transition-colors"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="mt-5 space-y-4">
          {/* Vehicle Selection */}
          <div>
            <label className="text-label-caps font-semibold text-on-surface flex items-center gap-1.5 mb-1.5">
              <Truck className="h-4 w-4 text-primary" />
              Reassign Vehicle
            </label>

            {isManualMode ? (
              <Input
                type="text"
                placeholder="e.g. 55555555-5555-5555-5555-555555555555"
                value={vehicleId}
                onChange={(e) => setVehicleId(e.target.value)}
                required
              />
            ) : fleetQuery.isLoading ? (
              <div className="h-10 w-full animate-pulse rounded-lg bg-slate-100" />
            ) : (
              <select
                id="select-edit-vehicle"
                aria-label="Select Vehicle"
                className="w-full rounded-lg border border-slate-300 bg-surface px-3 py-2.5 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 transition-all"
                value={vehicleId}
                onChange={(e) => setVehicleId(e.target.value)}
                required
              >
                <option value="">-- Choose an available vehicle --</option>
                {vehicles.map((v) => {
                  const isAvailable =
                    v.vehicleId === trip.vehicleId ||
                    (v.isAvailable && v.status !== "Maintenance" && v.status !== "Retired");
                  return (
                    <option key={v.vehicleId} value={v.vehicleId} disabled={!isAvailable}>
                      {v.registrationNo} — {v.vehicleType}
                      {v.capacityKg ? ` (${Number(v.capacityKg).toLocaleString()} kg)` : ""}
                      {v.vehicleId === trip.vehicleId
                        ? " [Currently Assigned]"
                        : !isAvailable
                        ? ` [${v.status || "Unavailable"}]`
                        : " [Available]"}
                    </option>
                  );
                })}
              </select>
            )}

            {selectedVehicle && !isManualMode && (
              <p className="mt-1 text-xs text-on-surface-variant">
                Registration: <strong className="text-on-surface">{selectedVehicle.registrationNo}</strong> • Type: {selectedVehicle.vehicleType}
              </p>
            )}
          </div>

          {/* Driver Selection */}
          <div>
            <label className="text-label-caps font-semibold text-on-surface flex items-center gap-1.5 mb-1.5">
              <User className="h-4 w-4 text-primary" />
              Reassign Driver
            </label>

            {isManualMode ? (
              <Input
                type="text"
                placeholder="e.g. 66666666-6666-6666-6666-666666666666"
                value={driverId}
                onChange={(e) => setDriverId(e.target.value)}
                required
              />
            ) : fleetQuery.isLoading ? (
              <div className="h-10 w-full animate-pulse rounded-lg bg-slate-100" />
            ) : (
              <select
                id="select-edit-driver"
                aria-label="Select Driver"
                className="w-full rounded-lg border border-slate-300 bg-surface px-3 py-2.5 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 transition-all"
                value={driverId}
                onChange={(e) => setDriverId(e.target.value)}
                required
              >
                <option value="">-- Choose an active driver --</option>
                {drivers.map((d) => {
                  const isActive = d.driverId === trip.driverId || d.status === "Active";
                  return (
                    <option key={d.driverId} value={d.driverId} disabled={!isActive}>
                      {d.fullName}
                      {d.licenceNo ? ` — Lic: ${d.licenceNo}` : ""}
                      {d.driverId === trip.driverId
                        ? " [Currently Assigned]"
                        : !isActive
                        ? ` [${d.status || "Inactive"}]`
                        : " [Active]"}
                    </option>
                  );
                })}
              </select>
            )}

            {selectedDriver && !isManualMode && (
              <p className="mt-1 text-xs text-on-surface-variant">
                Driver: <strong className="text-on-surface">{selectedDriver.fullName}</strong> • Licence: {selectedDriver.licenceNo || "N/A"}
              </p>
            )}
          </div>

          {/* Notes */}
          <div>
            <label className="block text-label-caps font-semibold text-on-surface mb-1.5">
              Reason for Reassignment / Notes (Optional)
            </label>
            <textarea
              rows={2}
              className="w-full rounded-lg border border-slate-300 bg-surface px-3 py-2 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 transition-all placeholder:text-on-surface-variant/50"
              placeholder="e.g. Original driver reported sick; swapped with standby driver."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              maxLength={500}
            />
          </div>

          {updateMutation.isError && (
            <div className="flex items-start gap-2 rounded-xl bg-red-50 p-3.5 text-xs text-red-700 border border-red-200">
              <AlertCircle className="h-4 w-4 shrink-0 text-red-600 mt-0.5" />
              <div>
                <p className="font-semibold">Unable to update trip assignments</p>
                <p className="mt-0.5">{getTripErrorMessage(updateMutation.error)}</p>
              </div>
            </div>
          )}

          <div className="flex items-center justify-between pt-4 border-t border-slate-100">
            <button
              type="button"
              onClick={() => setIsManualMode(!isManualMode)}
              className="inline-flex items-center gap-1.5 text-xs font-medium text-on-surface-variant hover:text-primary transition-colors"
            >
              <SlidersHorizontal className="h-3.5 w-3.5" />
              <span>{isManualMode ? "Use Dropdown Selectors" : "Enter IDs Manually"}</span>
            </button>

            <div className="flex items-center gap-2">
              <Button type="button" variant="secondary" onClick={onClose}>
                Cancel
              </Button>
              <Button
                type="submit"
                variant="primary"
                disabled={updateMutation.isPending || (!vehicleId && !driverId)}
              >
                {updateMutation.isPending ? "Saving..." : "Save Changes"}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

export default EditTripDialog;
