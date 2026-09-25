import { useState } from "react";
import { PlusCircle, X } from "lucide-react";
import Button from "../../../components/Button.jsx";
import Input from "../../../components/Input.jsx";
import { useCreateTripMutation } from "../api/tripsApi.js";
import { getTripErrorMessage } from "../lib/errorMessages.js";

function CreateTripDialog({ onClose, onCreated }) {
  const [assignmentId, setAssignmentId] = useState("");
  const [vehicleId, setVehicleId] = useState("");
  const [driverId, setDriverId] = useState("");
  const [notes, setNotes] = useState("");

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
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
    >
      <div className="relative w-full max-w-md rounded-xl bg-surface p-6 shadow-xl border border-slate-200">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-2 text-primary font-semibold text-title-md">
            <PlusCircle className="h-5 w-5" />
            <span>Dispatch New Trip</span>
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
          <div>
            <label className="block text-label-caps text-on-surface-variant mb-1">
              Assignment ID
            </label>
            <Input
              type="text"
              placeholder="e.g. 22222222-2222-2222-2222-222222222222"
              value={assignmentId}
              onChange={(e) => setAssignmentId(e.target.value)}
              required
            />
          </div>

          <div>
            <label className="block text-label-caps text-on-surface-variant mb-1">
              Vehicle ID
            </label>
            <Input
              type="text"
              placeholder="e.g. 55555555-5555-5555-5555-555555555555"
              value={vehicleId}
              onChange={(e) => setVehicleId(e.target.value)}
              required
            />
          </div>

          <div>
            <label className="block text-label-caps text-on-surface-variant mb-1">
              Driver ID
            </label>
            <Input
              type="text"
              placeholder="e.g. 66666666-6666-6666-6666-666666666666"
              value={driverId}
              onChange={(e) => setDriverId(e.target.value)}
              required
            />
          </div>

          <div>
            <label className="block text-label-caps text-on-surface-variant mb-1">
              Dispatch Instructions / Notes (Optional)
            </label>
            <textarea
              rows={2}
              className="w-full rounded-md border border-slate-300 bg-surface px-3 py-2 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
              placeholder="Special instructions for driver"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              maxLength={500}
            />
          </div>

          {createMutation.isError && (
            <div className="rounded-md bg-red-50 p-3 text-xs text-red-700 border border-red-200">
              {getTripErrorMessage(createMutation.error)}
            </div>
          )}

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <Button type="button" variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={createMutation.isPending || !assignmentId || !vehicleId || !driverId}
            >
              {createMutation.isPending ? "Creating..." : "Create Trip"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default CreateTripDialog;
