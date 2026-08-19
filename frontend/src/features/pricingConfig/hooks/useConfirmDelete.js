import { useState } from 'react'

// Tracks which row (by id) is in its inline "confirm delete?" state.
// Shared by FuelRateSection and VehicleEfficiencySection so only one row
// across the whole page can be mid-confirm at a time.
export function useConfirmDelete() {
  const [pendingId, setPendingId] = useState(null)

  return {
    pendingId,
    requestDelete: setPendingId,
    cancel: () => setPendingId(null),
  }
}
