import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  AlertTriangle,
  ArrowLeft,
  Building2,
  Edit2,
  MapPin,
  RefreshCw,
  Trash2,
  Truck,
  User,
} from "lucide-react";
import Card from "../../../components/Card.jsx";
import Button from "../../../components/Button.jsx";
import StatusBadge from "../../../components/StatusBadge.jsx";
import ErrorState from "../../../components/ErrorState.jsx";
import Skeleton from "../../../components/Skeleton.jsx";
import { useAppSelector } from "../../../hooks/useAppSelector.js";
import { TripStatus, UserRole } from "../../../lib/enums.js";
import { useTripDetailQuery } from "../api/tripsApi.js";
import { getTripStatusTone } from "../lib/statusTone.js";
import {
  formatAgencyName,
  formatDateTime,
  formatDriverName,
  formatTripId,
} from "../lib/format.js";
import { getTripErrorMessage } from "../lib/errorMessages.js";
import RouteMapCard from "../../loads/components/RouteMapCard.jsx";
import TripTimelineCard from "../components/TripTimelineCard.jsx";
import TripEvidenceCard from "../components/TripEvidenceCard.jsx";
import ChangeTripStatusDialog from "../components/ChangeTripStatusDialog.jsx";
import CancelTripDialog from "../components/CancelTripDialog.jsx";
import DeleteTripDialog from "../components/DeleteTripDialog.jsx";
import EditTripDialog from "../components/EditTripDialog.jsx";

function TripDetailPage() {
  const { tripId } = useParams();
  const navigate = useNavigate();
  const [isStatusDialogOpen, setIsStatusDialogOpen] = useState(false);
  const [isCancelDialogOpen, setIsCancelDialogOpen] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);

  const tripQuery = useTripDetailQuery(tripId, {
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status === "Assigned" || status === "PickedUp" || status === "InTransit") {
        return 4000;
      }
      return false;
    },
  });
  const role = useAppSelector((state) => state.auth.role);

  if (tripQuery.isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  if (tripQuery.isError) {
    return (
      <div className="space-y-6">
        <Link
          to="/trips"
          className="inline-flex items-center gap-1 text-body-md text-secondary hover:text-primary"
        >
          <ArrowLeft className="h-4 w-4" strokeWidth={1.5} /> Back to Trips
        </Link>
        <Card>
          <ErrorState
            description={getTripErrorMessage(tripQuery.error)}
            onRetry={tripQuery.refetch}
          />
        </Card>
      </div>
    );
  }

  const trip = tripQuery.data;
  if (!trip) return null;

  const canAdvanceStatus =
    (role === UserRole.AGENCY_STAFF || role === UserRole.DRIVER) &&
    trip.status !== TripStatus.DELIVERED &&
    trip.status !== TripStatus.CANCELLED &&
    trip.status !== "Delivered" &&
    trip.status !== "Cancelled";

  const canEdit =
    role === UserRole.AGENCY_STAFF &&
    (trip.status === TripStatus.ASSIGNED || trip.status === "Assigned");

  const canCancel =
    role === UserRole.AGENCY_STAFF &&
    trip.status !== TripStatus.DELIVERED &&
    trip.status !== TripStatus.CANCELLED &&
    trip.status !== "Delivered" &&
    trip.status !== "Cancelled";

  const canDelete =
    role === UserRole.AGENCY_STAFF &&
    trip.status !== TripStatus.DELIVERED &&
    trip.status !== "Delivered" &&
    (trip.status === TripStatus.CANCELLED ||
      trip.status === "Cancelled" ||
      trip.status === TripStatus.ASSIGNED ||
      trip.status === "Assigned");

  const hasRouteCoordinates =
    trip.pickupLat != null &&
    trip.pickupLng != null &&
    trip.dropoffLat != null &&
    trip.dropoffLng != null;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Link
            to="/trips"
            className="mb-2 inline-flex items-center gap-1 text-body-md text-secondary hover:text-primary"
          >
            <ArrowLeft className="h-4 w-4" strokeWidth={1.5} /> Back to Trips
          </Link>
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-headline-lg text-on-surface">
              Trip {formatTripId(trip.tripId)}
            </h1>
            <StatusBadge tone={getTripStatusTone(trip.status)}>
              {trip.status}
            </StatusBadge>
          </div>
          {trip.pickupAddress && trip.dropoffAddress && (
            <p className="mt-1 text-body-md text-on-surface-variant">
              {trip.pickupAddress} → {trip.dropoffAddress}
            </p>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="secondary"
            onClick={() => tripQuery.refetch()}
            disabled={tripQuery.isFetching}
            className="inline-flex items-center gap-1.5"
            title="Refresh trip monitor"
          >
            <RefreshCw className={`h-4 w-4 ${tripQuery.isFetching ? "animate-spin" : ""}`} />
            Refresh
          </Button>
          {canEdit && (
            <Button
              variant="secondary"
              onClick={() => setIsEditDialogOpen(true)}
              className="inline-flex items-center gap-1.5"
            >
              <Edit2 className="h-4 w-4" />
              Edit / Reassign
            </Button>
          )}
          {canAdvanceStatus && (
            <Button
              variant="primary"
              onClick={() => setIsStatusDialogOpen(true)}
              className="inline-flex items-center gap-1.5"
            >
              <RefreshCw className="h-4 w-4" />
              Update Status
            </Button>
          )}
          {canCancel && (
            <Button
              variant="secondary"
              onClick={() => setIsCancelDialogOpen(true)}
              className="inline-flex items-center gap-1.5"
              aria-label="Cancel Trip"
            >
              <AlertTriangle className="h-4 w-4 text-amber-600" />
              Cancel Trip
            </Button>
          )}
          {canDelete && (
            <Button
              variant="destructive"
              onClick={() => setIsDeleteDialogOpen(true)}
              className="inline-flex items-center gap-1.5"
              aria-label="Delete Trip"
            >
              <Trash2 className="h-4 w-4" />
              Delete Trip
            </Button>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Card>
            <h3 className="mb-4 text-headline-md text-primary">Trip Overview</h3>
            <dl className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <dt className="text-label-caps text-on-surface-variant flex items-center gap-1">
                  <Building2 className="h-3.5 w-3.5" /> Executing Agency
                </dt>
                <dd className="text-body-md text-on-surface mt-0.5">
                  {formatAgencyName(trip.agencyName, trip.agencyId)}
                </dd>
              </div>

              <div>
                <dt className="text-label-caps text-on-surface-variant flex items-center gap-1">
                  <User className="h-3.5 w-3.5" /> Assigned Driver
                </dt>
                <dd className="text-body-md text-on-surface mt-0.5">
                  {formatDriverName(trip.driverName, trip.driverId)}
                </dd>
              </div>

              <div>
                <dt className="text-label-caps text-on-surface-variant flex items-center gap-1">
                  <Truck className="h-3.5 w-3.5" /> Assigned Vehicle
                </dt>
                <dd className="text-data-mono text-on-surface mt-0.5">
                  {trip.vehicleRegistrationNo || formatTripId(trip.vehicleId)}
                </dd>
              </div>

              <div>
                <dt className="text-label-caps text-on-surface-variant">Created</dt>
                <dd className="text-data-mono text-on-surface mt-0.5">
                  {formatDateTime(trip.createdAt)}
                </dd>
              </div>

              {trip.pickupAddress && (
                <div className="sm:col-span-2">
                  <dt className="text-label-caps text-on-surface-variant flex items-center gap-1">
                    <MapPin className="h-3.5 w-3.5" /> Route
                  </dt>
                  <dd className="text-body-md text-on-surface mt-0.5">
                    {trip.pickupAddress} → {trip.dropoffAddress}
                  </dd>
                </div>
              )}
            </dl>
          </Card>

          {hasRouteCoordinates && (
            <RouteMapCard
              origin={trip.pickupAddress || "Pickup Location"}
              destination={trip.dropoffAddress || "Dropoff Location"}
              originLat={Number(trip.pickupLat)}
              originLng={Number(trip.pickupLng)}
              destinationLat={Number(trip.dropoffLat)}
              destinationLng={Number(trip.dropoffLng)}
            />
          )}

          <TripTimelineCard events={trip.events} />
        </div>

        <div className="space-y-6 lg:col-span-1">
          <TripEvidenceCard evidence={trip.evidence} />
        </div>
      </div>

      {isStatusDialogOpen && (
        <ChangeTripStatusDialog
          tripId={trip.tripId}
          currentStatus={trip.status}
          evidence={trip.evidence}
          onClose={() => setIsStatusDialogOpen(false)}
        />
      )}

      {isCancelDialogOpen && (
        <CancelTripDialog
          tripId={trip.tripId}
          onClose={() => setIsCancelDialogOpen(false)}
          onCancelled={() => tripQuery.refetch()}
        />
      )}

      {isDeleteDialogOpen && (
        <DeleteTripDialog
          tripId={trip.tripId}
          trip={trip}
          onClose={() => setIsDeleteDialogOpen(false)}
          onDeleted={() => {
            setIsDeleteDialogOpen(false);
            navigate("/trips");
          }}
        />
      )}

      {isEditDialogOpen && (
        <EditTripDialog
          trip={trip}
          onClose={() => setIsEditDialogOpen(false)}
        />
      )}
    </div>
  );
}

export default TripDetailPage;
