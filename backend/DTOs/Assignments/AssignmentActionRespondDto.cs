using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Assignments;

/// <summary>
/// Request payload for POST /api/v1/assignment-actions/respond - the public, token-gated
/// endpoint an agency's email Accept/Decline link submits to.
/// </summary>
public class AssignmentActionRespondDto
{
    [Required]
    [StringLength(128)]
    public string Token { get; set; } = string.Empty;
}
