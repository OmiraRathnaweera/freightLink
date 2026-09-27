import { useState } from "react";
import { CheckCircle2, MapPin, Package, ShieldCheck, Truck, X } from "lucide-react";
import { toast } from "sonner";
import Button from "../../../components/Button.jsx";
import { useApproveAssignmentMutation } from "../../trips/api/assignmentsApi.js";
import { formatCurrency, formatWeight } from "../lib/format.js";

/**
 * Modal dialog for an Agency to accept a posted load from the marketplace.
 * Adheres to Requirement 5 (load transitions to Matched and becomes private to this agency)
 * and enables Requirement 2 (agency can assign its registered drivers).
 */
export default function AcceptShipmentDialog({ load, onClose, onAccepted }) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  const approveMutation = useApproveAssignmentMutation({
    onSuccess: () => {
      toast.success("Shipment accepted! You can now assign a driver and vehicle to dispatch this trip.");
      onAccepted?.(load);
      onClose();
    },
    onError: (error) => {
      const msg = error?.response?.data?.error?.message || error?.message || "Failed to accept shipment.";
      toast.error(msg);
      setIsSubmitting(false);
    },
  });

  const handleConfirm = () => {
    setIsSubmitting(true);
    approveMutation.mutate({ id: load.loadId, data: {} });
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="accept-shipment-title"
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
    >
      <div className="relative w-full max-w-lg rounded-xl bg-surface p-6 shadow-xl border border-slate-200 animate-in fade-in zoom-in-95 duration-150">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-2.5 text-primary font-semibold text-title-md">
            <div className="p-1.5 rounded-lg bg-primary/10 text-primary">
              <CheckCircle2 className="h-5 w-5" />
            </div>
            <span id="accept-shipment-title">Accept Transport Shipment</span>
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

        <div className="mt-4 space-y-4">
          <p className="text-body-md text-on-surface-variant">
            Accepting this transport job will claim the shipment for your agency. It will be removed from the public marketplace and made available in your agency portal for driver assignment.
          </p>

          {/* Load Summary Card */}
          <div className="rounded-lg border border-slate-200 bg-slate-50/80 p-4 space-y-3 text-xs">
            <div className="flex items-center justify-between border-b border-slate-200 pb-2">
              <span className="font-semibold text-slate-700">Reference:</span>
              <span className="font-mono font-bold text-primary">{load.referenceCode}</span>
            </div>

            <div className="flex items-start gap-2 text-slate-700">
              <MapPin className="h-4 w-4 shrink-0 text-slate-400 mt-0.5" />
              <div>
                <div className="font-medium text-slate-900">{load.pickupAddress}</div>
                <div className="text-slate-400 text-[11px]">Destination: {load.dropoffAddress}</div>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-2 pt-1 border-t border-slate-200 text-slate-600">
              <div>
                <span className="text-slate-400">Cargo: </span>
                <span className="font-medium text-slate-800">{load.cargoDescription || "General freight"}</span>
              </div>
              <div>
                <span className="text-slate-400">Weight: </span>
                <span className="font-medium text-slate-800">{formatWeight(load.weightKg)}</span>
              </div>
            </div>

            {load.estimatedPrice != null && (
              <div className="flex items-center justify-between pt-1 border-t border-slate-200">
                <span className="font-semibold text-slate-700">Price / Agreed Rate:</span>
                <span className="font-mono font-bold text-emerald-700 text-sm">
                  {formatCurrency(load.estimatedPrice)}
                </span>
              </div>
            )}
          </div>

          <div className="rounded-md bg-blue-50 p-3 text-xs text-blue-800 border border-blue-200 flex items-start gap-2">
            <ShieldCheck className="h-4 w-4 shrink-0 mt-0.5 text-blue-600" />
            <span>
              <strong>Marketplace Rule:</strong> Once accepted, this load is exclusive to your agency. You may assign any of your registered drivers and vehicles to execute the trip.
            </span>
          </div>

          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
            <Button variant="secondary" onClick={onClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button
              variant="primary"
              onClick={handleConfirm}
              disabled={isSubmitting}
              className="inline-flex items-center gap-1.5"
            >
              <CheckCircle2 className="h-4 w-4" />
              {isSubmitting ? "Accepting..." : "Confirm & Accept Shipment"}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
