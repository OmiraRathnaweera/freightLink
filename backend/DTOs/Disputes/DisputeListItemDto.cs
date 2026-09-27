using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Row item DTO for paginated dispute listings.
/// </summary>
public class DisputeListItemDto
{
    /// <summary>The unique ID of the dispute.</summary>
    public Guid DisputeId { get; set; }

    /// <summary>The associated Trip ID.</summary>
    public Guid TripId { get; set; }

    /// <summary>The user ID of the user who raised the dispute.</summary>
    public Guid RaisedByUserId { get; set; }

    /// <summary>Safe claimant display details for the admin queue (never includes credentials).</summary>
    public string RaisedByName { get; set; } = string.Empty;
    public string RaisedByEmail { get; set; } = string.Empty;
    public string RaisedByRole { get; set; } = string.Empty;

    /// <summary>The category of the dispute.</summary>
    public DisputeCategory Category { get; set; }

    /// <summary>The detailed description of the dispute.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The current status of the dispute.</summary>
    public DisputeStatus Status { get; set; }

    /// <summary>Timestamp when the dispute was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Whether a formal resolution is attached to this dispute.</summary>
    public bool HasResolution { get; set; }

    public string? TripRouteSummary { get; set; }
    public string? CarrierAgencyName { get; set; }
    public string? VehicleRegistrationNo { get; set; }
    public DisputeResolutionResponseDto? Resolution { get; set; }
}
