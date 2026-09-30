using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Assignments;

/// <summary>
/// Optional request payload for declining a proposed assignment (POST /api/v1/assignments/{loadId}/decline).
/// </summary>
public class DeclineAssignmentDto
{
    /// <summary>Optional explanation or decline reason from Agency Staff.</summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}
