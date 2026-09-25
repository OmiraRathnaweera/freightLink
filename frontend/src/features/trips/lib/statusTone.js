import { TripStatus } from "../../../lib/enums.js";

// Maps a Trip's status (Assigned / PickedUp / InTransit / Delivered / Cancelled)
// to StatusBadge's accepted semantic tones ('neutral' | 'blue' | 'amber' | 'green' | 'red').
const TRIP_STATUS_TONE = {
  [TripStatus.ASSIGNED]: "neutral",
  [TripStatus.PICKED_UP]: "blue",
  [TripStatus.IN_TRANSIT]: "amber",
  [TripStatus.DELIVERED]: "green",
  [TripStatus.CANCELLED]: "red",
  // String fallbacks for wire enum casing
  Assigned: "neutral",
  PickedUp: "blue",
  InTransit: "amber",
  Delivered: "green",
  Cancelled: "red",
};

export function getTripStatusTone(status) {
  return TRIP_STATUS_TONE[status] ?? "neutral";
}
