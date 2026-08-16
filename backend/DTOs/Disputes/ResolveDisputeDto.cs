using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Request body for POST /api/v1/disputes/{id}/resolve.
/// </summary>
public class ResolveDisputeDto
{
    /// <summary>The formal outcome of the dispute resolution.</summary>
    [Required]
    public DisputeOutcome Outcome { get; set; }

    /// <summary>Resolution notes / justification by the adjudicator.</summary>
    [StringLength(2000, ErrorMessage = "Notes must not exceed 2000 characters.")]
    public string? Notes { get; set; }
}
