using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Request body for PUT /api/v1/disputes/{id}.
/// Only allowed while dispute is in Open status.
/// </summary>
public class UpdateDisputeDto
{
    /// <summary>The revised category of the dispute.</summary>
    [Required]
    public DisputeCategory Category { get; set; }

    /// <summary>Detailed description of the issue.</summary>
    [Required]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
    public string Description { get; set; } = string.Empty;
}
