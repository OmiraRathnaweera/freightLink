import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import AgenciesPage from "../../pages/AgenciesPage.jsx";
import { UserRole } from "../../../../lib/enums.js";
import { renderWithProviders } from "../../../../test/testUtils.jsx";
import * as agenciesApi from "../../../trips/api/agenciesApi.js";

vi.mock("../../../trips/api/agenciesApi.js", async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    useAgenciesQuery: vi.fn(),
    useAgencyFleetQuery: vi.fn(),
  };
});

function sampleAgency(overrides = {}) {
  return {
    agencyId: "11111111-1111-1111-1111-111111111111",
    name: "Colombo Express Logistics",
    businessRegNo: "BR-PV-10293",
    yardAddress: "123 Harbor Road, Colombo 13",
    yardLat: 6.945,
    yardLng: 79.855,
    status: "Active",
    driverCount: 14,
    activeDriverCount: 12,
    vehicleCount: 10,
    createdAt: "2026-08-01T10:00:00.000Z",
    updatedAt: "2026-08-01T10:00:00.000Z",
    ...overrides,
  };
}

function renderAgenciesPage(role = UserRole.ADMIN) {
  return renderWithProviders(<AgenciesPage />, {
    route: "/agencies",
    authState: { role, isAuthenticated: true },
  });
}

afterEach(() => {
  vi.mocked(agenciesApi.useAgenciesQuery).mockReset();
  vi.mocked(agenciesApi.useAgencyFleetQuery).mockReset();
  cleanup();
});

describe("AgenciesPage — Admin View", () => {
  it("renders loading skeletons while fetching agencies", () => {
    agenciesApi.useAgenciesQuery.mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
      isFetching: false,
      refetch: vi.fn(),
    });
    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
      isFetching: false,
      refetch: vi.fn(),
    });

    const { container } = renderAgenciesPage(UserRole.ADMIN);
    expect(container.querySelectorAll(".animate-pulse").length).toBeGreaterThan(0);
  });

  it("renders ErrorState when agencies query fails", async () => {
    const refetch = vi.fn();
    agenciesApi.useAgenciesQuery.mockReturnValue({
      isLoading: false,
      isError: true,
      error: new Error("Network timeout"),
      isFetching: false,
      refetch,
    });
    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
      isFetching: false,
      refetch: vi.fn(),
    });

    renderAgenciesPage(UserRole.ADMIN);
    expect(screen.getByText(/failed to load registered agencies/i)).toBeInTheDocument();
  });

  it("renders empty state when no agencies are registered", () => {
    agenciesApi.useAgenciesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [],
      isFetching: false,
      refetch: vi.fn(),
    });
    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
      isFetching: false,
      refetch: vi.fn(),
    });

    renderAgenciesPage(UserRole.ADMIN);
    expect(screen.getByText("No agencies found")).toBeInTheDocument();
  });

  it("renders registered agencies table with driver counts and details per Requirement 3", () => {
    const agency1 = sampleAgency({
      agencyId: "ag-1",
      name: "Colombo Express Logistics",
      driverCount: 18,
      activeDriverCount: 15,
      vehicleCount: 8,
    });
    const agency2 = sampleAgency({
      agencyId: "ag-2",
      name: "Kandy Transit Cargo",
      businessRegNo: "BR-KD-5512",
      driverCount: 6,
      activeDriverCount: 5,
      vehicleCount: 5,
      status: "Active",
    });

    agenciesApi.useAgenciesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [agency1, agency2],
      isFetching: false,
      refetch: vi.fn(),
    });
    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
      isFetching: false,
      refetch: vi.fn(),
    });

    renderAgenciesPage(UserRole.ADMIN);

    // Verify aggregate metrics
    expect(screen.getByText("Registered Agencies")).toBeInTheDocument();
    expect(screen.getByText("Total Drivers")).toBeInTheDocument();
    expect(screen.getByText("Fleet Vehicles")).toBeInTheDocument();

    // Total drivers aggregate: 18 + 6 = 24
    expect(screen.getByText("24")).toBeInTheDocument();
    // Total vehicles aggregate: 8 + 5 = 13
    expect(screen.getByText("13")).toBeInTheDocument();

    // Verify agency rows and driver counts
    expect(screen.getByText("Colombo Express Logistics")).toBeInTheDocument();
    expect(screen.getByText("Kandy Transit Cargo")).toBeInTheDocument();
    expect(screen.getByText("BR-KD-5512")).toBeInTheDocument();
    expect(screen.getByText("18")).toBeInTheDocument();
    expect(screen.getByText("6")).toBeInTheDocument();
  });

  it("displays dispatch rule indicating trip dispatch authority belongs only to agencies", () => {
    agenciesApi.useAgenciesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [],
      isFetching: false,
      refetch: vi.fn(),
    });
    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
      isFetching: false,
      refetch: vi.fn(),
    });

    renderAgenciesPage(UserRole.ADMIN);
    expect(screen.getByText(/Platform Dispatch Rule:/i)).toBeInTheDocument();
    expect(
      screen.getByText(/Trip dispatching authority is reserved exclusively to the executing agencies/i)
    ).toBeInTheDocument();
  });

  it("filters agencies table based on search input", async () => {
    const user = userEvent.setup();
    const agency1 = sampleAgency({ agencyId: "ag-1", name: "Colombo Express Logistics" });
    const agency2 = sampleAgency({ agencyId: "ag-2", name: "Galle Freight Lines" });

    agenciesApi.useAgenciesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [agency1, agency2],
      isFetching: false,
      refetch: vi.fn(),
    });
    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
      isFetching: false,
      refetch: vi.fn(),
    });

    renderAgenciesPage(UserRole.ADMIN);
    expect(screen.getByText("Colombo Express Logistics")).toBeInTheDocument();
    expect(screen.getByText("Galle Freight Lines")).toBeInTheDocument();

    const searchInput = screen.getByPlaceholderText(/search agency name/i);
    await user.type(searchInput, "Galle");

    expect(screen.queryByText("Colombo Express Logistics")).not.toBeInTheDocument();
    expect(screen.getByText("Galle Freight Lines")).toBeInTheDocument();
  });
});

describe("AgenciesPage — AgencyStaff View", () => {
  it("renders agency fleet overview for AgencyStaff", () => {
    agenciesApi.useAgenciesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: [],
      isFetching: false,
      refetch: vi.fn(),
    });
    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        agencyId: "ag-my",
        agencyName: "Lanka Freight Agency",
        drivers: [
          { driverId: "d-1", fullName: "Sunil Silva", licenceNo: "B12345", status: "Active" },
        ],
        vehicles: [
          { vehicleId: "v-1", registrationNo: "WP-CAB-1234", vehicleType: "Heavy Truck", status: "Available" },
        ],
      },
      isFetching: false,
      refetch: vi.fn(),
    });

    renderAgenciesPage(UserRole.AGENCY_STAFF);

    expect(screen.getByText(/Lanka Freight Agency — Fleet Roster/i)).toBeInTheDocument();
    expect(screen.getByText("Sunil Silva")).toBeInTheDocument();
    expect(screen.getByText("WP-CAB-1234")).toBeInTheDocument();
  });
});
