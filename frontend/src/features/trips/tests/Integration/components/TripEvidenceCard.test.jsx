import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import TripEvidenceCard from "../../../components/TripEvidenceCard.jsx";

describe("TripEvidenceCard", () => {
  it("renders pending state when evidence is empty", () => {
    render(<TripEvidenceCard evidence={[]} />);

    expect(screen.getByText("Trip Evidence")).toBeInTheDocument();
    expect(screen.getByText("Proof of Pickup")).toBeInTheDocument();
    expect(screen.getByText("Proof of Delivery")).toBeInTheDocument();

    const pendingBadges = screen.getAllByText("Pending");
    expect(pendingBadges).toHaveLength(2);
  });

  it("renders verified status when evidence items are present", () => {
    const evidence = [
      {
        tripEvidenceId: "ev-1",
        evidenceType: "PickupProof",
        storageKey: "pickup_key_123",
        capturedLat: 6.9271,
        capturedLng: 79.8612,
        capturedAt: "2026-09-11T10:00:00.000Z",
      },
    ];

    render(<TripEvidenceCard evidence={evidence} />);

    expect(screen.getByText("Verified")).toBeInTheDocument();
    expect(screen.getByText("Pending")).toBeInTheDocument();
    expect(screen.getByText("pickup_key_123")).toBeInTheDocument();
  });
});
