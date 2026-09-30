import { useState } from "react";
import { Loader2, MapPin, Send, X } from "lucide-react";
import { toast } from "sonner";
import Button from "../../../components/Button.jsx";
import Input from "../../../components/Input.jsx";
import { useCreateLoadProposalMutation } from "../api/loadProposalsApi.js";
import { formatCurrency, formatWeight } from "../lib/format.js";

/**
 * Modal dialog for an Agency to send a manual price proposal directly to the Shipper on a posted
 * load — an alternative to instantly claiming it from the marketplace. The Shipper reviews and
 * accepts one proposal manually; nothing changes here until they do.
 */
export default function SendProposalDialog({ load, onClose, onSent }) {
  const [proposedPrice, setProposedPrice] = useState(
    load.estimatedPrice != null ? String(load.estimatedPrice) : "",
  );
  const [message, setMessage] = useState("");

  const createMutation = useCreateLoadProposalMutation(load.loadId, {
    onSuccess: () => {
      toast.success("Proposal sent! The shipper will review your price and can accept it directly.");
      onSent?.(load);
      onClose();
    },
    onError: (error) => {
      const msg = error?.response?.data?.error?.message || error?.message || "Failed to send proposal.";
      toast.error(msg);
    },
  });

  function handleSubmit(e) {
    e.preventDefault();
    const price = Number(proposedPrice);
    if (!price || price <= 0) return;
    createMutation.mutate({ proposedPrice: price, message: message.trim() });
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="send-proposal-title"
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
    >
      <div className="relative w-full max-w-lg rounded-xl bg-surface p-6 shadow-xl border border-slate-200 animate-in fade-in zoom-in-95 duration-150">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center gap-2.5 text-primary font-semibold text-title-md">
            <div className="p-1.5 rounded-lg bg-primary/10 text-primary">
              <Send className="h-5 w-5" />
            </div>
            <span id="send-proposal-title">Send Price Proposal</span>
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
            Quote your price for this load. The shipper will see it alongside any other agencies'
            proposals and can accept yours directly — no need to wait for the automated match.
          </p>

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
                <span className="font-semibold text-slate-700">Shipper's rough estimate:</span>
                <span className="font-mono font-bold text-emerald-700 text-sm">
                  {formatCurrency(load.estimatedPrice)}
                </span>
              </div>
            )}
          </div>

          <div>
            <label htmlFor="proposal-price" className="block text-label-caps font-semibold text-on-surface mb-1.5">
              Your Proposed Price (LKR)
            </label>
            <Input
              id="proposal-price"
              type="number"
              min="0.01"
              step="any"
              placeholder="e.g. 42000"
              value={proposedPrice}
              onChange={(e) => setProposedPrice(e.target.value)}
              required
            />
          </div>

          <div>
            <label htmlFor="proposal-message" className="block text-label-caps font-semibold text-on-surface mb-1.5">
              Message to Shipper (Optional)
            </label>
            <textarea
              id="proposal-message"
              rows={2}
              maxLength={1000}
              className="w-full rounded-lg border border-slate-300 bg-surface px-3 py-2 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 transition-all placeholder:text-on-surface-variant/50"
              placeholder="e.g. We have a refrigerated truck available today."
              value={message}
              onChange={(e) => setMessage(e.target.value)}
            />
          </div>

          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
            <Button type="button" variant="secondary" onClick={onClose} disabled={createMutation.isPending}>
              Cancel
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={createMutation.isPending || !proposedPrice}
              className="inline-flex items-center gap-1.5"
            >
              {createMutation.isPending ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Sending...
                </>
              ) : (
                <>
                  <Send className="h-4 w-4" />
                  Send Proposal
                </>
              )}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
