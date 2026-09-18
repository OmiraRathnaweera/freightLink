// User-friendly copy for Trip error codes. Mirrors
// features/loads/lib/errorMessages.js exactly, including the same generic
// fallback for any unmapped code — which, today, is what actually handles
// GET /trips's current real-world response: TripService is still a stub
// (throws NotImplementedException), so every call currently comes back as
// INTERNAL_SERVER_ERROR. That code is deliberately NOT given a specific
// message below; it falls through to the generic fallback, which is
// already an honest, correct message for "the backend isn't finished yet"
// without this file needing to know that detail. The five codes below are
// TripsController's real, already-tested error codes (see
// TripsControllerTests.cs) — included now for when TripService is
// implemented, even though this basic list view won't trigger most of
// them itself.
const TRIP_ERROR_MESSAGES = {
  TRIP_NOT_FOUND: "This trip no longer exists.",
  TRIP_ACCESS_DENIED: "You don't have access to this trip.",
  INVALID_TRIP_STATUS_TRANSITION:
    "This trip cannot be moved to that status right now.",
  TRIP_EVIDENCE_REQUIRED:
    "Pickup or delivery evidence is required before this status change.",
  TRIP_EVIDENCE_ALREADY_EXISTS:
    "Evidence of this type has already been submitted for this trip.",
  TRIP_EVIDENCE_ROLE_MISMATCH:
    "You can't submit this type of evidence for this trip.",
};

export function getTripErrorMessage(error) {
  return (
    TRIP_ERROR_MESSAGES[error?.code] ??
    error?.message ??
    "Something went wrong. Please try again."
  );
}
