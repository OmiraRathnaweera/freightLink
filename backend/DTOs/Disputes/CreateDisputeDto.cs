using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Request body for POST /api/v1/disputes.
/// </summary>
public class CreateDisputeDto
{
    /// <summary>The ID of the trip being disputed.</summary>
    [Required]
    public Guid TripId { get; set; }

    /// <summary>The category of the dispute.</summary>
    [Required]
    [EnumDataType(typeof(DisputeCategory), ErrorMessage = "Category must be a valid DisputeCategory value.")]
    public DisputeCategory Category { get; set; }

    /// <summary>Detailed description of the issue.</summary>
    [Required]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
    public string Description { get; set; } = string.Empty;
}
