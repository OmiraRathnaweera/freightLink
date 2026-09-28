import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import AgenciesPage from "../../pages/AgenciesPage.jsx";
import { UserRole } from "../../../../lib/enums.js";
import { renderWithProviders } from "../../../../test/testUtils.jsx";
import * as agencyApi from "../../api/agencyApi.js";

vi.mock("../../api/agencyApi.js", async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    useAgenciesQuery: vi.fn(),
    useAgenciesSummaryQuery: vi.fn(),
    useAgencyFleetQuery: vi.fn(),
  };
});

// AgencyStaff's branch renders the full AgencyProfilePage (compliance docs,
// vehicles, profile editing) — that page has its own dedicated test suite
// (AgencyProfilePage.test.jsx), so it's stubbed here to isolate this file's
// concern: does AgenciesPage route each role to the right screen.
vi.mock("../../pages/AgencyProfilePage.jsx", () => ({
  default: () => <div>Agency Profile Page</div>,
}));

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

function pagedResponse(items, overrides = {}) {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalItems: items.length,
    totalPages: 1,
    ...overrides,
  };
}

function sampleSummary(overrides = {}) {
  return {
    totalAgencies: 2,
    activeAgencies: 2,
    totalDrivers: 24,
    activeDrivers: 20,
    totalVehicles: 13,
    ...overrides,
  };
}

function renderAgenciesPage(role = UserRole.ADMIN, options = {}) {
  return renderWithProviders(<AgenciesPage />, {
    route: "/agencies",
    authState: { role, isAuthenticated: true },
    ...options,
  });
}

function mockDefaultQueries({ agenciesData, summaryData } = {}) {
  agencyApi.useAgenciesQuery.mockReturnValue({
    isLoading: false,
    isError: false,
    data: agenciesData ?? pagedResponse([sampleAgency()]),
    isFetching: false,
    refetch: vi.fn(),
  });
  agencyApi.useAgenciesSummaryQuery.mockReturnValue({
    isLoading: false,
    isError: false,
    data: summaryData ?? sampleSummary(),
  });
  agencyApi.useAgencyFleetQuery.mockReturnValue({
    isLoading: false,
    isError: false,
    data: undefined,
    isFetching: false,
    refetch: vi.fn(),
  });
}

afterEach(() => {
  vi.mocked(agencyApi.useAgenciesQuery).mockReset();
  vi.mocked(agencyApi.useAgenciesSummaryQuery).mockReset();
  vi.mocked(agencyApi.useAgencyFleetQuery).mockReset();
  cleanup();
});

describe("AgenciesPage — Admin View", () => {
  it("renders loading skeletons while fetching agencies", () => {
    agencyApi.useAgenciesQuery.mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
      isFetching: false,
      refetch: vi.fn(),
    });
    agencyApi.useAgenciesSummaryQuery.mockReturnValue({ isLoading: true, isError: false, data: undefined });
    agencyApi.useAgencyFleetQuery.mockReturnValue({
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
    agencyApi.useAgenciesQuery.mockReturnValue({
      isLoading: false,
      isError: true,
      error: new Error("Network timeout"),
      isFetching: false,
      refetch,
    });
    agencyApi.useAgenciesSummaryQuery.mockReturnValue({ isLoading: false, isError: false, data: sampleSummary() });
    agencyApi.useAgencyFleetQuery.mockReturnValue({
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
    mockDefaultQueries({ agenciesData: pagedResponse([], { totalItems: 0, totalPages: 0 }), summaryData: sampleSummary({ totalAgencies: 0, activeAgencies: 0, totalDrivers: 0, activeDrivers: 0, totalVehicles: 0 }) });

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

    mockDefaultQueries({
      agenciesData: pagedResponse([agency1, agency2]),
      summaryData: sampleSummary({ totalDrivers: 24, totalVehicles: 13 }),
    });

    renderAgenciesPage(UserRole.ADMIN);

    // Verify aggregate metrics (from the system-wide summary endpoint, not the page's own rows)
    expect(screen.getByText("Registered Agencies")).toBeInTheDocument();
    expect(screen.getByText("Total Drivers")).toBeInTheDocument();
    expect(screen.getByText("Fleet Vehicles")).toBeInTheDocument();
    expect(screen.getByText("24")).toBeInTheDocument();
    expect(screen.getByText("13")).toBeInTheDocument();

    // Verify agency rows and driver counts
    expect(screen.getByText("Colombo Express Logistics")).toBeInTheDocument();
    expect(screen.getByText("Kandy Transit Cargo")).toBeInTheDocument();
    expect(screen.getByText("BR-KD-5512")).toBeInTheDocument();
    expect(screen.getByText("18")).toBeInTheDocument();
    expect(screen.getByText("6")).toBeInTheDocument();
  });

  it("summary cards reflect system-wide totals distinct from the current page's row count", () => {
    // Regression test for issue #45: only 1 agency is on this page, but the platform has 500 —
    // the summary cards must show the platform total, not this.data.items.length.
    mockDefaultQueries({
      agenciesData: pagedResponse([sampleAgency()], { totalItems: 500, totalPages: 25 }),
      summaryData: sampleSummary({ totalAgencies: 500, activeAgencies: 480, totalDrivers: 1200, activeDrivers: 1100, totalVehicles: 900 }),
    });

    renderAgenciesPage(UserRole.ADMIN);

    expect(screen.getByText("500")).toBeInTheDocument();
    expect(screen.getByText("1200")).toBeInTheDocument();
    expect(screen.getByText("900")).toBeInTheDocument();
  });

  it("displays dispatch rule indicating trip dispatch authority belongs only to agencies", () => {
    mockDefaultQueries({ agenciesData: pagedResponse([], { totalItems: 0, totalPages: 0 }) });

    renderAgenciesPage(UserRole.ADMIN);
    expect(screen.getByText(/Platform Dispatch Rule:/i)).toBeInTheDocument();
    expect(
      screen.getByText(/Trip dispatching authority is reserved exclusively to the executing agencies/i)
    ).toBeInTheDocument();
  });

  it("debounces search input before sending it to the API, and resets to page 1", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ delay: null });
    mockDefaultQueries();

    renderAgenciesPage(UserRole.ADMIN, { initialEntries: ["/agencies?page=3"] });

    const searchInput = screen.getByPlaceholderText(/search agency name/i);
    await user.type(searchInput, "Galle");

    // Not yet committed to the query — still debouncing.
    expect(agencyApi.useAgenciesQuery.mock.calls.at(-1)[0].search).toBeUndefined();

    await vi.advanceTimersByTimeAsync(500);

    await waitFor(() => {
      const lastCall = agencyApi.useAgenciesQuery.mock.calls.at(-1)[0];
      expect(lastCall.search).toBe("Galle");
      // Changing the search term resets the page back to 1.
      expect(lastCall.page).toBe(1);
    });

    vi.useRealTimers();
  });

  it("sends the selected status filter to the API and resets to page 1", async () => {
    const user = userEvent.setup();
    mockDefaultQueries();

    renderAgenciesPage(UserRole.ADMIN, { initialEntries: ["/agencies?page=2"] });

    const statusSelect = screen.getByDisplayValue("All Statuses");
    await user.selectOptions(statusSelect, "Suspended");

    await waitFor(() => {
      const lastCall = agencyApi.useAgenciesQuery.mock.calls.at(-1)[0];
      expect(lastCall.status).toBe("Suspended");
      expect(lastCall.page).toBe(1);
    });
  });

  it("renders pagination controls and requests the next page on click", async () => {
    const user = userEvent.setup();
    mockDefaultQueries({
      agenciesData: pagedResponse([sampleAgency()], { page: 1, pageSize: 20, totalItems: 45, totalPages: 3 }),
    });

    renderAgenciesPage(UserRole.ADMIN);

    expect(
      screen.getByText((_, element) => element?.textContent === "Showing 1–20 of 45")
    ).toBeInTheDocument();
    const nextPageButton = screen.getByRole("button", { name: /next page/i });
    await user.click(nextPageButton);

    await waitFor(() => {
      const lastCall = agencyApi.useAgenciesQuery.mock.calls.at(-1)[0];
      expect(lastCall.page).toBe(2);
    });
  });

  it("keeps the fleet inspection drawer open for the selected agency after the page changes", async () => {
    const user = userEvent.setup();
    const agency = sampleAgency({ agencyId: "ag-1", name: "Colombo Express Logistics" });
    mockDefaultQueries({
      agenciesData: pagedResponse([agency], { page: 1, pageSize: 20, totalItems: 45, totalPages: 3 }),
    });

    renderAgenciesPage(UserRole.ADMIN);

    await user.click(screen.getByRole("button", { name: /view fleet/i }));
    expect(screen.getByText("Agency Fleet Overview")).toBeInTheDocument();

    const nextPageButton = screen.getByRole("button", { name: /next page/i });
    await user.click(nextPageButton);

    // The drawer is keyed off selectedAgencyId local state, not the current page's rows, so it
    // must still be showing after paging away from the row that opened it.
    expect(screen.getByText("Agency Fleet Overview")).toBeInTheDocument();
  });
});

describe("AgenciesPage — AgencyStaff View", () => {
  it("routes AgencyStaff to AgencyProfilePage (compliance docs, vehicles, profile)", () => {
    mockDefaultQueries({ agenciesData: pagedResponse([], { totalItems: 0, totalPages: 0 }) });

    renderAgenciesPage(UserRole.AGENCY_STAFF);

    expect(screen.getByText("Agency Profile Page")).toBeInTheDocument();
    expect(screen.queryByText("Registered Agencies & Fleet")).not.toBeInTheDocument();
  });
});
