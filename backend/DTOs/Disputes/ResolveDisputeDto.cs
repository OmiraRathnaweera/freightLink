using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Request payload for resolving a dispute (PATCH/POST /api/disputes/{id}/resolve).
/// Requires a non-empty resolutionNote.
/// </summary>
public class ResolveDisputeDto
{
    /// <summary>The formal outcome of the dispute resolution. Defaults to Upheld.</summary>
    public DisputeOutcome Outcome { get; set; } = DisputeOutcome.Upheld;

    /// <summary>Mandatory resolution note explaining the resolution decision.</summary>
    [JsonPropertyName("resolutionNote")]
    public string? ResolutionNote { get; set; }

    /// <summary>Alias for ResolutionNote for backward compatibility.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// Returns the non-empty trimmed resolution note, checking both ResolutionNote and Notes properties.
    /// </summary>
    public string? GetEffectiveResolutionNote()
    {
        if (!string.IsNullOrWhiteSpace(ResolutionNote))
        {
            return ResolutionNote.Trim();
        }

        if (!string.IsNullOrWhiteSpace(Notes))
        {
            return Notes.Trim();
        }

        return null;
    }
}
