import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CreateTripDialog from "../../../components/CreateTripDialog.jsx";
import { renderWithProviders } from "../../../../../test/testUtils.jsx";
import * as tripsApi from "../../../api/tripsApi.js";
import * as assignmentsApi from "../../../api/assignmentsApi.js";
import * as agenciesApi from "../../../../agencies/api/agencyApi.js";

vi.mock("../../../api/tripsApi.js", async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    useCreateTripMutation: vi.fn(),
  };
});

vi.mock("../../../api/assignmentsApi.js", async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    useAssignmentsQuery: vi.fn(),
  };
});

vi.mock("../../../../agencies/api/agencyApi.js", async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    useAgencyFleetQuery: vi.fn(),
  };
});

const mockAssignments = [
  {
    assignmentId: "as-1111",
    loadId: "load-1111",
    agencyId: "agency-1111",
    referenceCode: "LD-2026-001",
    pickupAddress: "Colombo Port, Western",
    dropoffAddress: "Kandy Goods Yard, Central",
    cargoDescription: "Electronic Components",
    weightKg: 4500,
    volumeM3: 15,
    proposedPrice: 1250,
    status: "Accepted",
    tripId: null,
  },
  {
    assignmentId: "as-2222",
    loadId: "load-2222",
    agencyId: "agency-1111",
    referenceCode: "LD-2026-002",
    pickupAddress: "Galle Fort, Southern",
    dropoffAddress: "Jaffna Town, Northern",
    cargoDescription: "Construction Material",
    weightKg: 8000,
    volumeM3: 25,
    proposedPrice: 3400,
    status: "Accepted",
    tripId: "existing-trip-id", // Already has a trip
  },
];

const mockFleet = {
  agencyId: "agency-1111",
  agencyName: "Speedy Freight Ltd",
  vehicles: [
    {
      vehicleId: "veh-111",
      registrationNo: "WP-CAB-1234",
      vehicleType: "Box Truck",
      capacityKg: 5000,
      volumeM3: 20,
      status: "Available",
      isAvailable: true,
    },
    {
      vehicleId: "veh-222",
      registrationNo: "WP-CAB-5678",
      vehicleType: "Flatbed",
      capacityKg: 10000,
      volumeM3: 30,
      status: "Maintenance",
      isAvailable: false,
    },
  ],
  drivers: [
    {
      driverId: "drv-111",
      fullName: "Sunil Perera",
      email: "sunil@freightlink.lk",
      licenceNo: "B8839210",
      status: "Active",
      isActive: true,
    },
    {
      driverId: "drv-222",
      fullName: "Kamal Silva",
      email: "kamal@freightlink.lk",
      licenceNo: "B9934120",
      status: "Suspended",
      isActive: false,
    },
  ],
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("CreateTripDialog", () => {
  it("renders with available assignments, vehicles, and drivers in dropdowns", () => {
    assignmentsApi.useAssignmentsQuery.mockReturnValue({
      isLoading: false,
      data: { items: mockAssignments },
    });

    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      data: mockFleet,
    });

    tripsApi.useCreateTripMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      isError: false,
    });

    renderWithProviders(<CreateTripDialog onClose={vi.fn()} />);

    expect(screen.getByText("Dispatch New Trip")).toBeInTheDocument();
    expect(screen.getByText("1 ready to dispatch")).toBeInTheDocument();

    // Check Assignment dropdown has options
    const assignmentSelect = screen.getByRole("combobox", { name: /select assignment/i });
    expect(assignmentSelect).toBeInTheDocument();
    expect(screen.getByText(/LD-2026-001/)).toBeInTheDocument();

    // Check Vehicle dropdown has options
    const vehicleSelect = screen.getByRole("combobox", { name: /select vehicle/i });
    expect(vehicleSelect).toBeInTheDocument();
    expect(screen.getByText(/WP-CAB-1234/)).toBeInTheDocument();

    // Check Driver dropdown has options
    const driverSelect = screen.getByRole("combobox", { name: /select driver/i });
    expect(driverSelect).toBeInTheDocument();
    expect(screen.getByText(/Sunil Perera/)).toBeInTheDocument();
  });

  it("shows load preview card when an assignment is selected", async () => {
    assignmentsApi.useAssignmentsQuery.mockReturnValue({
      isLoading: false,
      data: { items: mockAssignments },
    });

    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      data: mockFleet,
    });

    tripsApi.useCreateTripMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      isError: false,
    });

    renderWithProviders(<CreateTripDialog onClose={vi.fn()} />);

    const assignmentSelect = screen.getByRole("combobox", { name: /select assignment/i });
    fireEvent.change(assignmentSelect, { target: { value: "as-1111" } });

    // Preview card appears
    expect(screen.getByText("LOAD LD-2026-001")).toBeInTheDocument();
    expect(screen.getByText("Colombo Port, Western")).toBeInTheDocument();
    expect(screen.getByText("Kandy Goods Yard, Central")).toBeInTheDocument();
    expect(screen.getByText("Electronic Components")).toBeInTheDocument();
  });

  it("submits the form with selected assignment, vehicle, and driver", async () => {
    const mutate = vi.fn();
    assignmentsApi.useAssignmentsQuery.mockReturnValue({
      isLoading: false,
      data: { items: mockAssignments },
    });

    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      data: mockFleet,
    });

    tripsApi.useCreateTripMutation.mockReturnValue({
      mutate,
      isPending: false,
      isError: false,
    });

    renderWithProviders(<CreateTripDialog onClose={vi.fn()} />);

    // Select assignment
    fireEvent.change(screen.getByRole("combobox", { name: /select assignment/i }), {
      target: { value: "as-1111" },
    });

    // Select vehicle
    fireEvent.change(screen.getByRole("combobox", { name: /select vehicle/i }), {
      target: { value: "veh-111" },
    });

    // Select driver
    fireEvent.change(screen.getByRole("combobox", { name: /select driver/i }), {
      target: { value: "drv-111" },
    });

    // Enter notes
    const notesInput = screen.getByPlaceholderText(/ensure cargo is securely strapped/i);
    await userEvent.type(notesInput, "Handle with care");

    // Click dispatch
    const submitBtn = screen.getByRole("button", { name: /create & dispatch trip/i });
    expect(submitBtn).not.toBeDisabled();
    await userEvent.click(submitBtn);

    expect(mutate).toHaveBeenCalledWith({
      assignmentId: "as-1111",
      vehicleId: "veh-111",
      driverId: "drv-111",
      notes: "Handle with care",
    });
  });

  it("never offers manual ID entry — only the dropdown selectors, even with empty lists", async () => {
    assignmentsApi.useAssignmentsQuery.mockReturnValue({
      isLoading: false,
      data: { items: [] },
    });

    agenciesApi.useAgencyFleetQuery.mockReturnValue({
      isLoading: false,
      data: { vehicles: [], drivers: [] },
    });

    tripsApi.useCreateTripMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      isError: false,
    });

    renderWithProviders(<CreateTripDialog onClose={vi.fn()} />);

    expect(screen.queryByRole("button", { name: /enter ids manually/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /enter an assignment id manually/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /enter vehicle id manually/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /enter driver id manually/i })).not.toBeInTheDocument();
    expect(screen.getByText(/no assignments found for your agency yet/i)).toBeInTheDocument();
    expect(screen.getByText(/no vehicles found in your agency fleet/i)).toBeInTheDocument();
    expect(screen.getByText(/no drivers registered in your agency fleet/i)).toBeInTheDocument();
  });
});
