import { useState } from "react";
import { Camera, CheckCircle2, Clock, ExternalLink, MapPin, X } from "lucide-react";
import Card from "../../../components/Card.jsx";
import { formatDateTime } from "../lib/format.js";

function getImageUrl(evidence) {
  if (!evidence) return null;
  const keyOrUrl = evidence.secureUrl || evidence.storageKey;
  if (!keyOrUrl) return null;
  if (keyOrUrl.startsWith("http://") || keyOrUrl.startsWith("https://")) {
    return keyOrUrl;
  }
  const apiBase = import.meta.env.VITE_API_BASE_URL || "http://localhost:5159/api/v1";
  const origin = apiBase.replace(/\/api\/v1\/?$/, "");
  if (keyOrUrl.startsWith("/")) {
    return `${origin}${keyOrUrl}`;
  }
  return `${origin}/api/v1/files/content/${keyOrUrl}`;
}

function EvidenceItem({ title, subtitle, evidence, requiredRole }) {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const imageUrl = evidence ? getImageUrl(evidence) : null;

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between gap-2 pb-3 border-b border-slate-100">
        <div>
          <h4 className="text-body-md font-semibold text-on-surface">{title}</h4>
          <p className="text-xs text-on-surface-variant">{subtitle}</p>
        </div>
        {evidence ? (
          <span className="inline-flex items-center gap-1 text-xs font-medium text-green-700 bg-green-50 px-2 py-0.5 rounded-full border border-green-200">
            <CheckCircle2 className="h-3.5 w-3.5 text-green-600" />
            Verified
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 text-xs font-medium text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full border border-amber-200">
            <Clock className="h-3.5 w-3.5 text-amber-600" />
            Pending
          </span>
        )}
      </div>

      {evidence ? (
        <div className="mt-3 space-y-3">
          <div
            onClick={() => setIsModalOpen(true)}
            className="group relative cursor-pointer overflow-hidden rounded-md border border-slate-200 bg-slate-100 aspect-video flex items-center justify-center hover:opacity-95 transition"
          >
            {imageUrl ? (
              <img
                src={imageUrl}
                alt={title}
                className="h-full w-full object-cover"
                onError={(e) => {
                  e.currentTarget.style.display = "none";
                  e.currentTarget.nextSibling.style.display = "flex";
                }}
              />
            ) : null}
            <div
              className="absolute inset-0 flex flex-col items-center justify-center p-3 text-center bg-slate-50 text-slate-500"
              style={{ display: imageUrl ? "none" : "flex" }}
            >
              <Camera className="h-8 w-8 mb-1 text-slate-400" />
              <span className="text-xs font-mono break-all">{evidence.storageKey}</span>
            </div>
            <div className="absolute inset-0 bg-black/30 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-1 text-white text-xs font-medium">
              <ExternalLink className="h-4 w-4" /> Click to view photo
            </div>
          </div>

          <div className="text-xs space-y-1 text-on-surface-variant">
            <div className="flex justify-between">
              <span>Captured:</span>
              <span className="font-mono text-on-surface">
                {formatDateTime(evidence.capturedAt)}
              </span>
            </div>
            {evidence.capturedLat != null && evidence.capturedLng != null && (
              <div className="flex items-center justify-between">
                <span>GPS Location:</span>
                <span className="inline-flex items-center gap-1 font-mono text-on-surface">
                  <MapPin className="h-3 w-3 text-slate-400" />
                  {Number(evidence.capturedLat).toFixed(4)}, {Number(evidence.capturedLng).toFixed(4)}
                </span>
              </div>
            )}
          </div>
        </div>
      ) : (
        <div className="mt-4 py-8 flex flex-col items-center justify-center text-center rounded-md bg-slate-50 border border-dashed border-slate-200 p-4">
          <Camera className="h-8 w-8 text-slate-300 mb-2" strokeWidth={1.5} />
          <p className="text-xs text-on-surface-variant max-w-[200px]">
            Awaiting upload by {requiredRole} during trip execution.
          </p>
        </div>
      )}

      {isModalOpen && imageUrl && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 p-4 backdrop-blur-xs"
          onClick={() => setIsModalOpen(false)}
        >
          <div
            className="relative max-w-3xl max-h-[90vh] bg-white rounded-lg overflow-hidden shadow-2xl p-2"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between p-2 border-b border-slate-100">
              <h3 className="text-body-md font-semibold text-on-surface">{title}</h3>
              <button
                type="button"
                onClick={() => setIsModalOpen(false)}
                className="rounded p-1 text-slate-400 hover:text-slate-700 hover:bg-slate-100"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="p-2 flex items-center justify-center max-h-[75vh] overflow-auto">
              <img
                src={imageUrl}
                alt={title}
                className="max-h-[70vh] w-auto object-contain rounded"
              />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/**
 * Card displaying Proof-of-Pickup and Proof-of-Delivery evidence photos.
 */
function TripEvidenceCard({ evidence = [] }) {
  const pickupEvidence = evidence.find(
    (e) => e.evidenceType === "PickupProof" || e.evidenceType === "pickupProof"
  );
  const deliveryEvidence = evidence.find(
    (e) => e.evidenceType === "DeliveryProof" || e.evidenceType === "deliveryProof"
  );

  return (
    <Card>
      <div className="flex items-center gap-2 mb-4">
        <Camera className="h-5 w-5 text-primary" strokeWidth={1.5} />
        <h3 className="text-headline-md text-primary">Trip Evidence</h3>
      </div>

      <div className="space-y-4">
        <EvidenceItem
          title="Proof of Pickup"
          subtitle="Captured at dispatch from yard"
          evidence={pickupEvidence}
          requiredRole="Agency Staff"
        />

        <EvidenceItem
          title="Proof of Delivery"
          subtitle="Captured upon delivery completion"
          evidence={deliveryEvidence}
          requiredRole="Driver"
        />
      </div>
    </Card>
  );
}

export default TripEvidenceCard;
