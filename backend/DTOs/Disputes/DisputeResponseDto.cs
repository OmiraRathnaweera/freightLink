using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Detailed response DTO for a single Dispute resource.
/// </summary>
public class DisputeResponseDto
{
    /// <summary>The unique ID of the dispute.</summary>
    public Guid DisputeId { get; set; }

    /// <summary>The associated Trip ID.</summary>
    public Guid TripId { get; set; }

    /// <summary>The user ID of the user who raised the dispute.</summary>
    public Guid RaisedByUserId { get; set; }

    /// <summary>Claimant details and safe trip context for a claimant-facing detail view.</summary>
    public string? RaisedByName { get; set; }
    public string? RaisedByRole { get; set; }
    public string? TripRouteSummary { get; set; }
    public string? CarrierAgencyName { get; set; }
    public string? VehicleRegistrationNo { get; set; }

    /// <summary>The category of the dispute.</summary>
    public DisputeCategory Category { get; set; }

    /// <summary>The detailed description of the dispute.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The current status of the dispute.</summary>
    public DisputeStatus Status { get; set; }

    /// <summary>Timestamp when the dispute was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp when the dispute was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Resolution details if the dispute is resolved.</summary>
    public DisputeResolutionResponseDto? Resolution { get; set; }
}
