import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DeleteTripDialog from "../../components/DeleteTripDialog.jsx";
import * as tripsApi from "../../api/tripsApi.js";

vi.mock("../../api/tripsApi.js", async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    useDeleteTripMutation: vi.fn(),
  };
});

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

afterEach(() => {
  vi.mocked(tripsApi.useDeleteTripMutation).mockReset();
  cleanup();
});

describe("DeleteTripDialog", () => {
  const sampleTrip = {
    tripId: "11111111-2222-3333-4444-555555555555",
    status: "Assigned",
    driverName: "Sunil Silva",
    vehicleRegistrationNo: "WP-CAB-9988",
  };

  it("renders dialog with title, trip identifier and permanent warning note", () => {
    tripsApi.useDeleteTripMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      isError: false,
    });

    render(
      <DeleteTripDialog
        tripId={sampleTrip.tripId}
        trip={sampleTrip}
        onClose={vi.fn()}
      />
    );

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Delete Trip")).toBeInTheDocument();
    expect(screen.getByText("Sunil Silva")).toBeInTheDocument();
    expect(screen.getByText("WP-CAB-9988")).toBeInTheDocument();
    expect(screen.getByText(/permanent and cannot be undone/i)).toBeInTheDocument();
  });

  it("submits permanent deletion when confirmed", async () => {
    const mutate = vi.fn();
    tripsApi.useDeleteTripMutation.mockReturnValue({
      mutate,
      isPending: false,
      isError: false,
    });

    const onClose = vi.fn();

    render(
      <DeleteTripDialog
        tripId={sampleTrip.tripId}
        trip={sampleTrip}
        onClose={onClose}
      />
    );

    const deleteBtn = screen.getByRole("button", { name: /permanently delete trip/i });
    await userEvent.click(deleteBtn);

    expect(mutate).toHaveBeenCalledWith({
      tripId: sampleTrip.tripId,
    });
  });

  it("closes dialog when Keep Trip or close icon is clicked", async () => {
    tripsApi.useDeleteTripMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      isError: false,
    });

    const onClose = vi.fn();

    render(
      <DeleteTripDialog
        tripId={sampleTrip.tripId}
        trip={sampleTrip}
        onClose={onClose}
      />
    );

    const keepBtn = screen.getByRole("button", { name: /keep trip/i });
    await userEvent.click(keepBtn);
    expect(onClose).toHaveBeenCalledTimes(1);

    const closeIconBtn = screen.getByRole("button", { name: /close/i });
    await userEvent.click(closeIconBtn);
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it("displays loading state while deletion is pending", () => {
    tripsApi.useDeleteTripMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: true,
      isError: false,
    });

    render(
      <DeleteTripDialog
        tripId={sampleTrip.tripId}
        trip={sampleTrip}
        onClose={vi.fn()}
      />
    );

    expect(screen.getByText("Deleting...")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /deleting/i })).toBeDisabled();
    expect(screen.getByRole("button", { name: /keep trip/i })).toBeDisabled();
  });

  it("displays error message if deletion fails", () => {
    tripsApi.useDeleteTripMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      isError: true,
      error: {
        code: "INVALID_TRIP_STATUS_TRANSITION",
        message: "Cannot cancel a trip in 'Delivered' status.",
      },
    });

    render(
      <DeleteTripDialog
        tripId={sampleTrip.tripId}
        trip={sampleTrip}
        onClose={vi.fn()}
      />
    );

    expect(
      screen.getByText("This trip cannot be moved to that status right now.")
    ).toBeInTheDocument();
  });
});
