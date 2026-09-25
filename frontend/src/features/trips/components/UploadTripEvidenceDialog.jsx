import { useState } from "react";
import { AlertCircle, Camera, Loader2, MapPin, Upload, X } from "lucide-react";
import Button from "../../../components/Button.jsx";
import Input from "../../../components/Input.jsx";
import { EvidenceType, UserRole } from "../../../lib/enums.js";
import { uploadSingleFile, useUploadTripEvidenceMutation } from "../api/tripsApi.js";
import { getTripErrorMessage } from "../lib/errorMessages.js";

function UploadTripEvidenceDialog({ tripId, userRole, evidence = [], onClose, onUploaded }) {
  const hasPickup = evidence?.some(
    (e) => e.evidenceType === EvidenceType.PICKUP_PROOF || e.evidenceType === "PickupProof"
  );
  const hasDelivery = evidence?.some(
    (e) => e.evidenceType === EvidenceType.DELIVERY_PROOF || e.evidenceType === "DeliveryProof"
  );

  const defaultEvidenceType =
    userRole === UserRole.DRIVER
      ? EvidenceType.DELIVERY_PROOF
      : userRole === UserRole.AGENCY_STAFF
      ? EvidenceType.PICKUP_PROOF
      : hasPickup
      ? EvidenceType.DELIVERY_PROOF
      : EvidenceType.PICKUP_PROOF;

  const [evidenceType, setEvidenceType] = useState(defaultEvidenceType);
  const [selectedFile, setSelectedFile] = useState(null);
  const [capturedLat, setCapturedLat] = useState("");
  const [capturedLng, setCapturedLng] = useState("");
  const [isLocating, setIsLocating] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [errorMsg, setErrorMsg] = useState(null);

  const isAlreadyUploaded =
    (evidenceType === EvidenceType.PICKUP_PROOF && hasPickup) ||
    (evidenceType === EvidenceType.DELIVERY_PROOF && hasDelivery);

  const uploadMutation = useUploadTripEvidenceMutation({
    onSuccess: () => {
      if (onUploaded) onUploaded();
      onClose();
    },
    onError: (err) => {
      setErrorMsg(getTripErrorMessage(err));
    },
  });

  function handleGetLocation() {
    if (!navigator.geolocation) {
      setErrorMsg("Geolocation is not supported by your browser.");
      return;
    }
    setIsLocating(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setCapturedLat(pos.coords.latitude.toFixed(6));
        setCapturedLng(pos.coords.longitude.toFixed(6));
        setIsLocating(false);
      },
      (err) => {
        setErrorMsg(err.message || "Failed to retrieve location.");
        setIsLocating(false);
      },
      { timeout: 10000, enableHighAccuracy: true }
    );
  }

  async function handleSubmit(e) {
    e.preventDefault();
    if (isAlreadyUploaded) return;

    if (!selectedFile) {
      setErrorMsg("Please select an evidence photo to upload.");
      return;
    }

    setIsUploading(true);
    setErrorMsg(null);

    try {
      // Step 1: Upload file to file storage (local fallback or Cloudinary)
      const uploadRes = await uploadSingleFile(selectedFile);
      const publicId = uploadRes.publicId;

      // Step 2: Link evidence to trip
      await uploadMutation.mutateAsync({
        tripId,
        data: {
          publicId,
          evidenceType,
          capturedLat: capturedLat ? Number(capturedLat) : undefined,
          capturedLng: capturedLng ? Number(capturedLng) : undefined,
        },
      });
    } catch (err) {
      setErrorMsg(getTripErrorMessage(err) || err.message || "Failed to upload evidence.");
    } finally {
      setIsUploading(false);
    }
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
    >
      <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-xl">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-2">
            <Camera className="h-5 w-5 text-primary" />
            <h3 className="text-headline-md text-on-surface">Upload Trip Evidence</h3>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="mt-4 space-y-4">
          <div>
            <label className="block text-sm font-medium text-on-surface mb-1">
              Evidence Type
            </label>
            <select
              value={evidenceType}
              onChange={(e) => setEvidenceType(e.target.value)}
              disabled={userRole === UserRole.AGENCY_STAFF || userRole === UserRole.DRIVER}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border disabled:bg-slate-100"
            >
              <option value={EvidenceType.PICKUP_PROOF}>Proof of Pickup (Agency Staff)</option>
              <option value={EvidenceType.DELIVERY_PROOF}>Proof of Delivery (Driver)</option>
            </select>
          </div>

          {isAlreadyUploaded ? (
            <div className="rounded-md bg-amber-50 p-3 text-xs text-amber-800 border border-amber-200 flex items-start gap-2">
              <AlertCircle className="h-4 w-4 text-amber-600 mt-0.5 shrink-0" />
              <div>
                <span className="font-semibold">Evidence Already Uploaded:</span> Proof of{" "}
                {evidenceType === EvidenceType.PICKUP_PROOF ? "pickup" : "delivery"} has already been
                submitted for this trip.
              </div>
            </div>
          ) : (
            <>
              <div>
                <label className="block text-sm font-medium text-on-surface mb-1">
                  Evidence Photo
                </label>
                <div className="flex flex-col items-center justify-center rounded-lg border-2 border-dashed border-slate-300 p-4 text-center hover:border-primary transition-colors bg-slate-50">
                  <Upload className="h-8 w-8 text-slate-400 mb-2" />
                  <input
                    type="file"
                    accept="image/*,.pdf"
                    onChange={(e) => setSelectedFile(e.target.files?.[0] || null)}
                    className="text-xs text-on-surface file:mr-2 file:rounded-md file:border-0 file:bg-primary file:px-3 file:py-1 file:text-xs file:font-semibold file:text-white hover:file:bg-primary/90 cursor-pointer"
                  />
                  {selectedFile && (
                    <p className="mt-2 text-xs font-medium text-primary break-all">
                      Selected: {selectedFile.name} ({(selectedFile.size / 1024).toFixed(1)} KB)
                    </p>
                  )}
                </div>
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
                    value={capturedLat}
                    onChange={(e) => setCapturedLat(e.target.value)}
                  />
                  <Input
                    type="number"
                    step="any"
                    placeholder="Longitude"
                    value={capturedLng}
                    onChange={(e) => setCapturedLng(e.target.value)}
                  />
                </div>
              </div>
            </>
          )}

          {errorMsg && (
            <div className="rounded-md bg-red-50 p-3 text-xs text-red-700 border border-red-200">
              {errorMsg}
            </div>
          )}

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
            <Button type="button" variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={isUploading || uploadMutation.isPending || !selectedFile || isAlreadyUploaded}
            >
              {isUploading ? "Uploading..." : "Submit Evidence"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default UploadTripEvidenceDialog;
