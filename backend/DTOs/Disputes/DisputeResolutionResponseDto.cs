using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Resolution record attached to a resolved dispute.
/// </summary>
public class DisputeResolutionResponseDto
{
    /// <summary>The ID of the associated dispute.</summary>
    public Guid DisputeId { get; set; }

    /// <summary>The user ID of the Admin/Staff member who resolved the dispute.</summary>
    public Guid ResolvedByUserId { get; set; }

    /// <summary>The outcome of the resolution.</summary>
    public DisputeOutcome Outcome { get; set; }

    /// <summary>Resolution notes / justification.</summary>
    public string? Notes { get; set; }

    /// <summary>Timestamp when the dispute was resolved.</summary>
    public DateTimeOffset ResolvedAt { get; set; }
}
