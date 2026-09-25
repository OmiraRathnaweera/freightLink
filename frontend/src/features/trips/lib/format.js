// Shared display formatters for the Trips pages — mirrors
// features/loads/lib/format.js's approach: one place per fallback so the
// table (and any future trip detail page) can never drift apart.
//
// formatDateTime is duplicated here rather than imported from
// features/loads/lib/format.js — it's genuinely generic (not Load-specific
// despite living in that file), but this codebase's feature folders read
// as self-contained (see LoadsTable.jsx's own comment on
// .claude/rules/frontend-design.md #1), and no cross-feature import
// precedent was available to confirm before writing this. If the team
// prefers a shared src/lib/format.js for cases like this, this is a small,
// easy find-and-replace later.
export function formatDateTime(isoDateTime) {
  return new Date(isoDateTime).toLocaleString("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

// TripListItemDto has no tripReferenceCode — just the raw tripId — so this
// shortens it the same way formatShipperName shortens an unresolved
// shipperUserId, for a readable (if not human-friendly) table cell.
export function formatTripId(tripId) {
  if (!tripId) return "Unknown";
  return `${tripId.slice(0, 8)}…`;
}

// TripListItemDto currently returns only a raw agencyId — no agencyName
// field exists on this endpoint yet (unlike Load's shipperName). Every
// call today falls through to the id-based fallback; this function exists
// so that changes nothing here once/if the backend adds a real name.
export function formatAgencyName(agencyName, agencyId) {
  if (agencyName && agencyName.trim()) return agencyName;
  if (agencyId) return `Unknown (${agencyId.slice(0, 8)}…)`;
  return "Unknown";
}

// Same reasoning as formatAgencyName, for TripListItemDto's driverId.
export function formatDriverName(driverName, driverId) {
  if (driverName && driverName.trim()) return driverName;
  if (driverId) return `Unknown (${driverId.slice(0, 8)}…)`;
  return "Unknown";
}
