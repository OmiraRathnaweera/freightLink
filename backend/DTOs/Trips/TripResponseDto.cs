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

    /// <summary>The assigned vehicle's id.</summary>
    public Guid VehicleId { get; set; }

    /// <summary>The assigned driver's id.</summary>
    public Guid DriverId { get; set; }

    /// <summary>The trip's current status (e.g. "Assigned", "PickedUp", "InTransit", "Delivered").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the trip was created (i.e. when the assignment was accepted).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the trip was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>The trip's full status-change timeline, newest first.</summary>
    public List<TripEventResponseDto> Events { get; set; } = new();

    /// <summary>Captured proof-of-pickup / proof-of-delivery evidence for this trip (at most one row per <c>EvidenceType</c>).</summary>
    public List<TripEvidenceResponseDto> Evidence { get; set; } = new();
}