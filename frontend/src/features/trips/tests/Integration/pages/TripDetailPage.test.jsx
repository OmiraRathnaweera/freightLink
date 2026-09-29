import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import TripDetailPage from "../../../pages/TripDetailPage.jsx";
import { UserRole } from "../../../../../lib/enums.js";
import { renderWithProviders } from "../../../../../test/testUtils.jsx";
import * as tripsApi from "../../../api/tripsApi.js";

vi.mock("../../../api/tripsApi.js", async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    useTripDetailQuery: vi.fn(),
    useChangeTripStatusMutation: vi.fn(),
    useUpdateTripMutation: vi.fn(),
    useCancelTripMutation: vi.fn(),
    useDeleteTripMutation: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
  };
});

// Mock RouteMapCard to prevent Leaflet DOM initialization issues in jsdom
vi.mock("../../../../loads/components/RouteMapCard.jsx", () => ({
  default: () => <div data-testid="route-map-mock">Route Map</div>,
}));

function sampleTripDetail(overrides = {}) {
  return {
    tripId: "11111111-1111-1111-1111-111111111111",
    assignmentId: "22222222-2222-2222-2222-222222222222",
    loadId: "33333333-3333-3333-3333-333333333333",
    agencyId: "44444444-4444-4444-4444-444444444444",
    agencyName: "Speedy Logistics",
    vehicleId: "55555555-5555-5555-5555-555555555555",
    vehicleRegistrationNo: "WP-CAB-1234",
    driverId: "66666666-6666-6666-6666-666666666666",
    driverName: "John Driver",
    pickupAddress: "Colombo Port",
    dropoffAddress: "Kandy Yard",
    pickupLat: 6.9400,
    pickupLng: 79.8500,
    dropoffLat: 7.2906,
    dropoffLng: 80.6337,
    status: "Assigned",
    createdAt: "2026-09-10T09:00:00.000Z",
    updatedAt: "2026-09-10T09:00:00.000Z",
    events: [
      {
        tripEventId: "ev-1",
        recordedByUserId: "user-1",
        fromStatus: null,
        toStatus: "Assigned",
        notes: "Trip assigned",
        snapshotLat: null,
        snapshotLng: null,
        occurredAt: "2026-09-10T09:00:00.000Z",
      },
    ],
    evidence: [],
    ...overrides,
  };
}

function renderTripDetailPage(tripId = "11111111-1111-1111-1111-111111111111", role = UserRole.AGENCY_STAFF) {
  return renderWithProviders(<TripDetailPage />, {
    route: "/trips/:tripId",
    initialEntries: [`/trips/${tripId}`],
    authState: { role, isAuthenticated: true },
  });
}

afterEach(() => {
  vi.mocked(tripsApi.useTripDetailQuery).mockReset();
  vi.mocked(tripsApi.useChangeTripStatusMutation).mockReset();
  vi.mocked(tripsApi.useUpdateTripMutation).mockReset();
  vi.mocked(tripsApi.useCancelTripMutation).mockReset();
  cleanup();
});

describe("TripDetailPage — render states", () => {
  it("renders loading skeleton while fetching trip", () => {
    tripsApi.useTripDetailQuery.mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
    });

    const { container } = renderTripDetailPage();
    expect(container.querySelectorAll(".animate-pulse").length).toBeGreaterThan(0);
  });

  it("renders error state when trip fetch fails", () => {
    tripsApi.useTripDetailQuery.mockReturnValue({
      isLoading: false,
      isError: true,
      error: new Error("Trip not found"),
      refetch: vi.fn(),
    });

    renderTripDetailPage();
    expect(screen.getByText("Trip not found")).toBeInTheDocument();
  });

  it("renders trip details, overview, timeline, and evidence cards", () => {
    const trip = sampleTripDetail();
    tripsApi.useTripDetailQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: trip,
    });
    tripsApi.useChangeTripStatusMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    });

    renderTripDetailPage();

    expect(screen.getByText("Trip Overview")).toBeInTheDocument();
    expect(screen.getByText("Speedy Logistics")).toBeInTheDocument();
    expect(screen.getByText("John Driver")).toBeInTheDocument();
    expect(screen.getByText("WP-CAB-1234")).toBeInTheDocument();
    expect(screen.getByText("Status & Timeline Monitor")).toBeInTheDocument();
    expect(screen.getByText("Trip Evidence")).toBeInTheDocument();
    expect(screen.getByText("Proof of Pickup")).toBeInTheDocument();
    expect(screen.getByText("Proof of Delivery")).toBeInTheDocument();
  });

  it("opens status dialog when Update Status is clicked", async () => {
    const trip = sampleTripDetail();
    tripsApi.useTripDetailQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: trip,
    });
    tripsApi.useChangeTripStatusMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    });

    renderTripDetailPage();

    const updateBtn = screen.getByRole("button", { name: /update status/i });
    await userEvent.click(updateBtn);

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Next Status")).toBeInTheDocument();
  });

  it("opens edit dialog when Edit / Reassign is clicked", async () => {
    const trip = sampleTripDetail({ status: "Assigned" });
    tripsApi.useTripDetailQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: trip,
    });
    tripsApi.useUpdateTripMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    });

    renderTripDetailPage();

    const editBtn = screen.getByRole("button", { name: /edit \/ reassign/i });
    await userEvent.click(editBtn);

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Edit Trip Assignments")).toBeInTheDocument();
  });

  it("opens cancel dialog when Cancel Trip is clicked", async () => {
    const trip = sampleTripDetail({ status: "Assigned" });
    tripsApi.useTripDetailQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: trip,
    });
    tripsApi.useCancelTripMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    });

    renderTripDetailPage();

    const cancelBtn = screen.getByRole("button", { name: /cancel trip/i });
    await userEvent.click(cancelBtn);

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText(/confirm cancellation/i)).toBeInTheDocument();
  });

  it("opens delete dialog when Delete Trip is clicked on a Cancelled trip", async () => {
    const trip = sampleTripDetail({ status: "Cancelled" });
    tripsApi.useTripDetailQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: trip,
    });

    renderTripDetailPage();

    expect(screen.queryByRole("button", { name: /cancel trip/i })).not.toBeInTheDocument();

    const deleteBtn = screen.getByRole("button", { name: /delete trip/i });
    expect(deleteBtn).toBeInTheDocument();
    await userEvent.click(deleteBtn);

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /permanently delete trip/i })).toBeInTheDocument();
  });
});
