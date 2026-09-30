namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// Full single-resource response for <c>GET /api/v1/trips/{id}</c> — per the API contract (Section
/// 4.4), this is "trip detail incl. evidence + timeline", so unlike <see cref="TripListItemDto"/> it
/// includes the full <see cref="Events"/> and <see cref="Evidence"/> collections. Never exposes the
/// <c>Trip</c> entity directly.
/// </summary>
public class TripResponseDto
{
    /// <summary>The trip's unique id.</summary>
    public Guid TripId { get; set; }

    /// <summary>The accepted <c>Assignment</c> this trip was created from.</summary>
    public Guid AssignmentId { get; set; }

    /// <summary>The originating load's id, denormalized from <c>Trip.Assignment.LoadId</c>.</summary>
    public Guid LoadId { get; set; }

    /// <summary>The executing agency's id, denormalized from <c>Trip.Assignment.AgencyId</c>.</summary>
    public Guid AgencyId { get; set; }

    /// <summary>The executing agency's name, if resolved.</summary>
    public string? AgencyName { get; set; }

    /// <summary>The assigned vehicle's id.</summary>
    public Guid VehicleId { get; set; }

    /// <summary>The assigned vehicle's registration number, if resolved.</summary>
    public string? VehicleRegistrationNo { get; set; }

    /// <summary>The assigned driver's id.</summary>
    public Guid DriverId { get; set; }

    /// <summary>The assigned driver's full name, if resolved.</summary>
    public string? DriverName { get; set; }

    /// <summary>Origin / pickup address from the load.</summary>
    public string? PickupAddress { get; set; }

    /// <summary>Destination / dropoff address from the load.</summary>
    public string? DropoffAddress { get; set; }

    /// <summary>Pickup latitude.</summary>
    public decimal? PickupLat { get; set; }

    /// <summary>Pickup longitude.</summary>
    public decimal? PickupLng { get; set; }

    /// <summary>Dropoff latitude.</summary>
    public decimal? DropoffLat { get; set; }

    /// <summary>Dropoff longitude.</summary>
    public decimal? DropoffLng { get; set; }

    /// <summary>The trip's current status (e.g. "Assigned", "PickedUp", "InTransit", "Delivered").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the trip was created (i.e. when the assignment was accepted).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the trip was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Originating cargo description from the load.</summary>
    public string? CargoDescription { get; set; }

    /// <summary>Total cargo weight in kilograms.</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Total cargo volume in cubic meters.</summary>
    public decimal? VolumeM3 { get; set; }

    /// <summary>Pickup window start time.</summary>
    public DateTimeOffset? PickupWindowStart { get; set; }

    /// <summary>Pickup window end time.</summary>
    public DateTimeOffset? PickupWindowEnd { get; set; }

    /// <summary>Shipper load reference code (e.g. LD-CMB-KDY-01).</summary>
    public string? ReferenceCode { get; set; }

    /// <summary>The owning shipper's user id, denormalized from <c>Trip.Assignment.Load.ShipperUserId</c>.</summary>
    public Guid? ShipperUserId { get; set; }

    /// <summary>The owning shipper's display name, if resolved.</summary>
    public string? ShipperName { get; set; }

    /// <summary>
    /// The agreed price for this job, from the accepted <c>Assignment.ProposedPrice</c> (the job
    /// proposal / AI-matched price the Shipper already approved). Once the trip is Delivered, this is
    /// the default amount Agency Staff bills via Create Invoice — see <c>InvoiceService.CreateAsync</c>.
    /// </summary>
    public decimal? AgreedPrice { get; set; }

    /// <summary>Routed distance in kilometers from assignment.</summary>
    public decimal? RoutedDistanceKm { get; set; }

    /// <summary>Proposed ETA in minutes from assignment.</summary>
    public int? ProposedEtaMinutes { get; set; }

    /// <summary>The trip's full status-change timeline, newest first.</summary>
    public List<TripEventResponseDto> Events { get; set; } = new();

    /// <summary>Captured proof-of-pickup / proof-of-delivery evidence for this trip (at most one row per <c>EvidenceType</c>).</summary>
    public List<TripEvidenceResponseDto> Evidence { get; set; } = new();
}