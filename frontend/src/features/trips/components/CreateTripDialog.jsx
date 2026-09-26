import { useState, useMemo, useEffect } from "react";
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  MapPin,
  Package,
  PlusCircle,
  SlidersHorizontal,
  Truck,
  User,
  X,
} from "lucide-react";
import Button from "../../../components/Button.jsx";
import Input from "../../../components/Input.jsx";
import { useCreateTripMutation } from "../api/tripsApi.js";
import { useAssignmentsQuery } from "../api/assignmentsApi.js";
import { useAgencyFleetQuery } from "../../agencies/api/agencyApi.js";
import { getTripErrorMessage } from "../lib/errorMessages.js";

function CreateTripDialog({ onClose, onCreated, defaultAssignmentId = "" }) {
  const [assignmentId, setAssignmentId] = useState(defaultAssignmentId);
  const [vehicleId, setVehicleId] = useState("");
  const [driverId, setDriverId] = useState("");
  const [notes, setNotes] = useState("");
  const [isManualMode, setIsManualMode] = useState(false);

  // Fetch all assignments for the agency
  const assignmentsQuery = useAssignmentsQuery(
    { pageSize: 100, sortBy: "createdAt", sortDir: "desc" },
    { staleTime: 30000 }
  );

  const assignments = useMemo(
    () => assignmentsQuery.data?.items ?? [],
    [assignmentsQuery.data?.items]
  );

  // Group assignments into dispatchable (no trip yet) vs already dispatched
  const { dispatchableAssignments, alreadyDispatchedAssignments } = useMemo(() => {
    const dispatchable = [];
    const dispatched = [];

    assignments.forEach((a) => {
      if (a.tripId) {
        dispatched.push(a);
      } else if (a.status !== "Declined" && a.status !== "Cancelled") {
        dispatchable.push(a);
      }
    });

    return {
      dispatchableAssignments: dispatchable,
      alreadyDispatchedAssignments: dispatched,
    };
  }, [assignments]);

  // Selected assignment detail — matches by assignmentId or loadId
  const selectedAssignment = useMemo(
    () => assignments.find((a) => a.assignmentId === assignmentId || a.loadId === assignmentId),
    [assignments, assignmentId]
  );

  useEffect(() => {
    if (selectedAssignment && selectedAssignment.assignmentId !== assignmentId) {
      setAssignmentId(selectedAssignment.assignmentId);
    }
  }, [selectedAssignment, assignmentId]);

  // Fetch fleet (vehicles and drivers) for caller's agency
  const fleetQuery = useAgencyFleetQuery(selectedAssignment?.agencyId, {
    staleTime: 60000,
  });

  const vehicles = useMemo(
    () => fleetQuery.data?.vehicles ?? [],
    [fleetQuery.data?.vehicles]
  );
  const drivers = useMemo(
    () => fleetQuery.data?.drivers ?? [],
    [fleetQuery.data?.drivers]
  );

  const selectedVehicle = useMemo(
    () => vehicles.find((v) => v.vehicleId === vehicleId),
    [vehicles, vehicleId]
  );

  const selectedDriver = useMemo(
    () => drivers.find((d) => d.driverId === driverId),
    [drivers, driverId]
  );

  const createMutation = useCreateTripMutation({
    onSuccess: (data) => {
      if (onCreated) onCreated(data);
      onClose();
    },
  });

  function handleSubmit(e) {
    e.preventDefault();
    if (!assignmentId || !vehicleId || !driverId) return;

    createMutation.mutate({
      assignmentId: assignmentId.trim(),
      vehicleId: vehicleId.trim(),
      driverId: driverId.trim(),
      notes: notes.trim() || undefined,
    });
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs overflow-y-auto"
    >
      <div className="relative w-full max-w-lg rounded-2xl bg-surface p-6 shadow-2xl border border-slate-200 my-8">
        {/* Header */}
        <div className="flex items-center justify-between pb-4 border-b border-slate-100">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <PlusCircle className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-title-md font-bold text-on-surface">Dispatch New Trip</h2>
              <p className="text-body-sm text-on-surface-variant">
                Assign a vehicle and driver to an accepted load assignment.
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
          {/* STEP 1: Assignment Selection */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="text-label-caps font-semibold text-on-surface flex items-center gap-1.5">
                <Package className="h-4 w-4 text-primary" />
                Select Assignment / Load
              </label>
              {!isManualMode && dispatchableAssignments.length > 0 && (
                <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">
                  {dispatchableAssignments.length} ready to dispatch
                </span>
              )}
            </div>

            {isManualMode ? (
              <Input
                type="text"
                placeholder="e.g. 22222222-2222-2222-2222-222222222222"
                value={assignmentId}
                onChange={(e) => setAssignmentId(e.target.value)}
                required
              />
            ) : assignmentsQuery.isLoading ? (
              <div className="h-10 w-full animate-pulse rounded-lg bg-slate-100" />
            ) : dispatchableAssignments.length === 0 && alreadyDispatchedAssignments.length === 0 ? (
              <div className="rounded-lg border border-dashed border-slate-200 bg-slate-50/50 p-4 text-center">
                <p className="text-body-sm text-on-surface-variant">
                  No assignments found for your agency yet.
                </p>
                <button
                  type="button"
                  onClick={() => setIsManualMode(true)}
                  className="mt-2 text-xs font-medium text-primary hover:underline"
                >
                  Enter an Assignment ID manually
                </button>
              </div>
            ) : (
              <select
                id="select-assignment"
                aria-label="Select Assignment"
                className="w-full rounded-lg border border-slate-300 bg-surface px-3 py-2.5 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 transition-all"
                value={assignmentId}
                onChange={(e) => setAssignmentId(e.target.value)}
                required
              >
                <option value="">-- Choose an assignment to dispatch --</option>
                {dispatchableAssignments.map((a) => (
                  <option key={a.assignmentId} value={a.assignmentId}>
                    {a.referenceCode ? `[${a.referenceCode}] ` : ""}
                    {a.pickupAddress ? a.pickupAddress.split(",")[0] : "Origin"} ➔{" "}
                    {a.dropoffAddress ? a.dropoffAddress.split(",")[0] : "Destination"}
                    {a.cargoDescription ? ` • ${a.cargoDescription}` : ""}
                    {a.proposedPrice ? ` ($${Number(a.proposedPrice).toLocaleString()})` : ""}
                  </option>
                ))}

                {alreadyDispatchedAssignments.length > 0 && (
                  <optgroup label="⚠️ Already Dispatched (Trip Created)">
                    {alreadyDispatchedAssignments.map((a) => (
                      <option key={a.assignmentId} value={a.assignmentId} disabled>
                        {a.referenceCode ? `[${a.referenceCode}] ` : ""}
                        {a.pickupAddress ? a.pickupAddress.split(",")[0] : "Origin"} ➔{" "}
                        {a.dropoffAddress ? a.dropoffAddress.split(",")[0] : "Destination"}{" "}
                        (Trip #{a.tripId?.substring(0, 8)} already exists)
                      </option>
                    ))}
                  </optgroup>
                )}
              </select>
            )}

            {/* Selected Assignment Preview Card */}
            {selectedAssignment && !isManualMode && (
              <div className="mt-2.5 rounded-xl border border-primary/20 bg-primary/5 p-3.5 space-y-2.5">
                <div className="flex items-center justify-between text-xs font-medium text-primary">
                  <span className="font-semibold tracking-wide">
                    {selectedAssignment.referenceCode ? `LOAD ${selectedAssignment.referenceCode}` : "ASSIGNMENT"}
                  </span>
                  <span className="px-2 py-0.5 rounded-md bg-primary/10 font-semibold">
                    ${Number(selectedAssignment.proposedPrice || 0).toLocaleString()}
                  </span>
                </div>

                <div className="flex items-center gap-2 text-body-sm text-on-surface font-medium">
                  <MapPin className="h-4 w-4 text-emerald-600 shrink-0" />
                  <span className="truncate">{selectedAssignment.pickupAddress || "Pickup location"}</span>
                  <ArrowRight className="h-3.5 w-3.5 text-on-surface-variant shrink-0" />
                  <MapPin className="h-4 w-4 text-rose-600 shrink-0" />
                  <span className="truncate">{selectedAssignment.dropoffAddress || "Dropoff location"}</span>
                </div>

                <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-on-surface-variant pt-1 border-t border-primary/10">
                  {selectedAssignment.cargoDescription && (
                    <span>Cargo: <strong className="text-on-surface">{selectedAssignment.cargoDescription}</strong></span>
                  )}
                  {selectedAssignment.weightKg && (
                    <span>Weight: <strong className="text-on-surface">{selectedAssignment.weightKg.toLocaleString()} kg</strong></span>
                  )}
                  {selectedAssignment.routedDistanceKm && (
                    <span>Distance: <strong className="text-on-surface">{selectedAssignment.routedDistanceKm} km</strong></span>
                  )}
                </div>
              </div>
            )}
          </div>

          {/* STEP 2: Vehicle Selection */}
          <div>
            <label className="text-label-caps font-semibold text-on-surface flex items-center gap-1.5 mb-1.5">
              <Truck className="h-4 w-4 text-primary" />
              Assign Fleet Vehicle
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
            ) : vehicles.length === 0 ? (
              <div className="rounded-lg border border-dashed border-slate-200 bg-slate-50 p-3 text-body-sm text-on-surface-variant">
                No vehicles found in your agency fleet.{" "}
                <button
                  type="button"
                  onClick={() => setIsManualMode(true)}
                  className="font-medium text-primary hover:underline"
                >
                  Enter Vehicle ID manually
                </button>
              </div>
            ) : (
              <select
                id="select-vehicle"
                aria-label="Select Vehicle"
                className="w-full rounded-lg border border-slate-300 bg-surface px-3 py-2.5 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 transition-all"
                value={vehicleId}
                onChange={(e) => setVehicleId(e.target.value)}
                required
              >
                <option value="">-- Choose an available vehicle --</option>
                {vehicles.map((v) => {
                  const isAvailable = v.isAvailable && v.status !== "Maintenance" && v.status !== "Retired";
                  return (
                    <option key={v.vehicleId} value={v.vehicleId} disabled={!isAvailable}>
                      {v.registrationNo} — {v.vehicleType}
                      {v.capacityKg ? ` (${Number(v.capacityKg).toLocaleString()} kg)` : ""}
                      {!isAvailable ? ` [${v.status || "Unavailable"}]` : " [Available]"}
                    </option>
                  );
                })}
              </select>
            )}

            {selectedVehicle && !isManualMode && (
              <p className="mt-1 text-xs text-on-surface-variant">
                Registration: <strong className="text-on-surface">{selectedVehicle.registrationNo}</strong> • Type: {selectedVehicle.vehicleType} • Capacity: {selectedVehicle.capacityKg} kg
              </p>
            )}
          </div>

          {/* STEP 3: Driver Selection */}
          <div>
            <label className="text-label-caps font-semibold text-on-surface flex items-center gap-1.5 mb-1.5">
              <User className="h-4 w-4 text-primary" />
              Assign Driver
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
            ) : drivers.length === 0 ? (
              <div className="rounded-lg border border-dashed border-slate-200 bg-slate-50 p-3 text-body-sm text-on-surface-variant">
                No drivers registered in your agency fleet.{" "}
                <button
                  type="button"
                  onClick={() => setIsManualMode(true)}
                  className="font-medium text-primary hover:underline"
                >
                  Enter Driver ID manually
                </button>
              </div>
            ) : (
              <select
                id="select-driver"
                aria-label="Select Driver"
                className="w-full rounded-lg border border-slate-300 bg-surface px-3 py-2.5 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 transition-all"
                value={driverId}
                onChange={(e) => setDriverId(e.target.value)}
                required
              >
                <option value="">-- Choose an active driver --</option>
                {drivers.map((d) => {
                  const isActive = d.isActive && d.status === "Active";
                  return (
                    <option key={d.driverId} value={d.driverId} disabled={!isActive}>
                      {d.fullName}
                      {d.licenceNo ? ` — Lic: ${d.licenceNo}` : ""}
                      {!isActive ? ` [${d.status || "Inactive"}]` : " [Active]"}
                    </option>
                  );
                })}
              </select>
            )}

            {selectedDriver && !isManualMode && (
              <p className="mt-1 text-xs text-on-surface-variant">
                Driver: <strong className="text-on-surface">{selectedDriver.fullName}</strong> • Licence: {selectedDriver.licenceNo || "N/A"} • Email: {selectedDriver.email}
              </p>
            )}
          </div>

          {/* STEP 4: Instructions / Notes */}
          <div>
            <label className="block text-label-caps font-semibold text-on-surface mb-1.5">
              Dispatch Instructions / Driver Notes (Optional)
            </label>
            <textarea
              rows={2}
              className="w-full rounded-lg border border-slate-300 bg-surface px-3 py-2 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 transition-all placeholder:text-on-surface-variant/50"
              placeholder="e.g. Ensure cargo is securely strapped; contact consignee upon arrival."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              maxLength={500}
            />
          </div>

          {/* Error Message */}
          {createMutation.isError && (
            <div className="flex items-start gap-2 rounded-xl bg-red-50 p-3.5 text-xs text-red-700 border border-red-200">
              <AlertCircle className="h-4 w-4 shrink-0 text-red-600 mt-0.5" />
              <div>
                <p className="font-semibold">Unable to dispatch trip</p>
                <p className="mt-0.5">{getTripErrorMessage(createMutation.error)}</p>
              </div>
            </div>
          )}

          {/* Footer Controls */}
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
                disabled={createMutation.isPending || !assignmentId || !vehicleId || !driverId}
                className="inline-flex items-center gap-2 font-semibold shadow-sm"
              >
                {createMutation.isPending ? (
                  <>
                    <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                    <span>Dispatching...</span>
                  </>
                ) : (
                  <>
                    <CheckCircle2 className="h-4 w-4" />
                    <span>Create & Dispatch Trip</span>
                  </>
                )}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

export default CreateTripDialog;
