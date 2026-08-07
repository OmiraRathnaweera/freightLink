// Placeholder — protected route (any authenticated role).
// Covers both trip and assignment views for now — no separate "assignments"
// feature folder exists in the scaffold, and AssignmentStatus/TripStatus
// (src/lib/enums.js) both belong to this feature domain.
function TripsPage() {
  return (
    <div>
      <h1>Trips</h1>
      <p>Placeholder page — trip/assignment views not implemented yet.</p>
    </div>
  )
}

export default TripsPage
