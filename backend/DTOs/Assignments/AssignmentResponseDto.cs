namespace FreightLink.Api.DTOs.Assignments;

/// <summary>
/// Detailed response DTO for an Assignment / Job Proposal (GET /api/v1/assignments/{id}).
/// Contains load details, proposed price, ETA, distance, and status.
/// </summary>
public class AssignmentResponseDto
{
    /// <summary>The assignment's unique id.</summary>
    public Guid AssignmentId { get; set; }

    /// <summary>The associated load id.</summary>
    public Guid LoadId { get; set; }

    /// <summary>The matched agency id.</summary>
    public Guid AgencyId { get; set; }

    /// <summary>The matched agency name, if resolved.</summary>
    public string? AgencyName { get; set; }

    /// <summary>Associated workflow run id.</summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>Proposed agency payout / price computed by Agent 3.</summary>
    public decimal ProposedPrice { get; set; }

    /// <summary>Routed distance in kilometers from ORS.</summary>
    public decimal? RoutedDistanceKm { get; set; }

    /// <summary>Proposed transit duration in minutes.</summary>
    public int? ProposedEtaMinutes { get; set; }

    /// <summary>Current assignment status: Proposed, Accepted, Declined.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Cargo description from the load.</summary>
    public string? CargoDescription { get; set; }

    /// <summary>Cargo weight in kilograms.</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Cargo volume in cubic meters.</summary>
    public decimal? VolumeM3 { get; set; }

    /// <summary>Pickup location address.</summary>
    public string? PickupAddress { get; set; }

    /// <summary>Pickup latitude.</summary>
    public decimal? PickupLat { get; set; }

    /// <summary>Pickup longitude.</summary>
    public decimal? PickupLng { get; set; }

    /// <summary>Dropoff location address.</summary>
    public string? DropoffAddress { get; set; }

    /// <summary>Dropoff latitude.</summary>
    public decimal? DropoffLat { get; set; }

    /// <summary>Dropoff longitude.</summary>
    public decimal? DropoffLng { get; set; }

    /// <summary>Start of pickup window.</summary>
    public DateTimeOffset? PickupWindowStart { get; set; }

    /// <summary>End of pickup window.</summary>
    public DateTimeOffset? PickupWindowEnd { get; set; }

    /// <summary>Shipper user full name or business name, if resolved.</summary>
    public string? ShipperName { get; set; }

    /// <summary>Shipper reference code for the load.</summary>
    public string? ReferenceCode { get; set; }

    /// <summary>Existing trip id if a trip has already been created for this assignment.</summary>
    public Guid? TripId { get; set; }

    /// <summary>When the assignment was proposed.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the assignment was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
