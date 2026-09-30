namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// Lightweight row shape for <c>GET /api/v1/trips</c> list results. Omits per-event/per-evidence
/// detail (see <see cref="TripResponseDto"/> for the full detail shape returned by
/// <c>GET /api/v1/trips/{id}</c>), matching the same list-vs-detail split already established by
/// <see cref="Loads.LoadListItemDto"/>/<see cref="Loads.LoadResponseDto"/>.
/// </summary>
public class TripListItemDto
{
    /// <summary>The trip's unique id.</summary>
    public Guid TripId { get; set; }

    /// <summary>The accepted <c>Assignment</c> this trip was created from.</summary>
    public Guid AssignmentId { get; set; }

    /// <summary>
    /// The originating load's id, denormalized from <c>Trip.Assignment.LoadId</c> so list consumers
    /// (e.g. a Shipper's own-loads view) don't need a second round trip per row.
    /// </summary>
    public Guid LoadId { get; set; }

    /// <summary>
    /// The executing agency's id, denormalized from <c>Trip.Assignment.AgencyId</c> — see
    /// <see cref="LoadId"/> for why this is included on the list row rather than detail-only.
    /// </summary>
    public Guid AgencyId { get; set; }

    /// <summary>The executing agency's name, if resolved.</summary>
    public string? AgencyName { get; set; }

    /// <summary>The assigned vehicle's id.</summary>
    public Guid VehicleId { get; set; }

    /// <summary>The assigned driver's id.</summary>
    public Guid DriverId { get; set; }

    /// <summary>The assigned driver's full name, if resolved.</summary>
    public string? DriverName { get; set; }

    /// <summary>Origin / pickup address from the load, denormalized for list views.</summary>
    public string? PickupAddress { get; set; }

    /// <summary>Destination / dropoff address from the load, denormalized for list views.</summary>
    public string? DropoffAddress { get; set; }

    /// <summary>Shipper load reference code (e.g. LD-CMB-KDY-01).</summary>
    public string? ReferenceCode { get; set; }

    /// <summary>The trip's current status (e.g. "Assigned", "PickedUp", "InTransit", "Delivered").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the trip was created (i.e. when the assignment was accepted).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the trip was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}