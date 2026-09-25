using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Request payload for unified status transition (PATCH /api/disputes/{id}/status).
/// </summary>
public class TransitionDisputeStatusDto
{
    /// <summary>Target status to transition to.</summary>
    [Required]
    [JsonPropertyName("status")]
    public DisputeStatus Status { get; set; }

    /// <summary>Resolution note required when transitioning to Resolved.</summary>
    [JsonPropertyName("resolutionNote")]
    public string? ResolutionNote { get; set; }

    /// <summary>Alias for resolutionNote.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>Optional outcome when resolving (defaults to Upheld).</summary>
    [JsonPropertyName("outcome")]
    public DisputeOutcome? Outcome { get; set; }

    /// <summary>Returns the non-empty trimmed resolution note.</summary>
    public string? GetEffectiveResolutionNote()
    {
        if (!string.IsNullOrWhiteSpace(ResolutionNote))
            return ResolutionNote.Trim();
        if (!string.IsNullOrWhiteSpace(Notes))
            return Notes.Trim();
        return null;
    }
}
