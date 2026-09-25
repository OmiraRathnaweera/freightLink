import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import TripsPage from "../../pages/TripsPage.jsx";
import { UserRole } from "../../../../lib/enums.js";
import { renderWithProviders } from "../../../../test/testUtils.jsx";
import * as tripsApi from "../../api/tripsApi.js";

vi.mock("../../api/tripsApi.js", async (importOriginal) => {
  const actual = await importOriginal();
  return { ...actual, useTripsQuery: vi.fn() };
});

function sampleTrip(overrides = {}) {
  return {
    tripId: "11111111-1111-1111-1111-111111111111",
    assignmentId: "22222222-2222-2222-2222-222222222222",
    loadId: "33333333-3333-3333-3333-333333333333",
    agencyId: "44444444-4444-4444-4444-444444444444",
    agencyName: "Speedy Logistics",
    vehicleId: "55555555-5555-5555-5555-555555555555",
    driverId: "66666666-6666-6666-6666-666666666666",
    driverName: "John Driver",
    status: "Assigned",
    createdAt: "2026-09-10T09:00:00.000Z",
    updatedAt: "2026-09-10T09:00:00.000Z",
    ...overrides,
  };
}

function renderTripsPage(role = UserRole.ADMIN) {
  return renderWithProviders(<TripsPage />, {
    route: "/trips",
    authState: { role, isAuthenticated: true },
  });
}

afterEach(() => {
  vi.mocked(tripsApi.useTripsQuery).mockReset();
  cleanup();
});

describe("TripsPage — loading state", () => {
  it("renders skeleton while loading", () => {
    tripsApi.useTripsQuery.mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
    });
    const { container } = renderTripsPage();
    expect(container.querySelectorAll(".animate-pulse").length).toBeGreaterThan(0);
    expect(screen.queryByText("No active trips")).not.toBeInTheDocument();
  });
});

describe("TripsPage — error state", () => {
  it("renders ErrorState with retry button when query fails", async () => {
    const refetch = vi.fn();
    tripsApi.useTripsQuery.mockReturnValue({
      isLoading: false,
      isError: true,
      error: new Error("Network timeout"),
      refetch,
    });

    renderTripsPage();
    expect(screen.getByText("Network timeout")).toBeInTheDocument();

    const retryBtn = screen.getByRole("button", { name: /retry/i });
    await userEvent.click(retryBtn);
    expect(refetch).toHaveBeenCalledTimes(1);
  });
});

describe("TripsPage — empty state", () => {
  it("renders EmptyState when no trips exist", () => {
    tripsApi.useTripsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 },
    });

    renderTripsPage();
    expect(screen.getByText("No active trips")).toBeInTheDocument();
  });
});

describe("TripsPage — loaded state with table", () => {
  it("renders trips table and links to trip detail", () => {
    const trip = sampleTrip();
    tripsApi.useTripsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [trip], page: 1, pageSize: 20, totalItems: 1, totalPages: 1 },
    });

    renderTripsPage();
    expect(screen.getByText("Speedy Logistics")).toBeInTheDocument();
    expect(screen.getByText("John Driver")).toBeInTheDocument();
    expect(screen.getAllByText("Assigned").length).toBeGreaterThanOrEqual(1);

    const detailLinks = screen.getAllByRole("link", { name: /view/i });
    expect(detailLinks[0]).toHaveAttribute("href", `/trips/${trip.tripId}`);
  });
});
