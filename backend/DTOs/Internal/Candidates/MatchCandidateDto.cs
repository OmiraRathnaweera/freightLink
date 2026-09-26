using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Internal.Candidates;

/// <summary>
/// DTO for persisting an evaluated candidate carrier under an AgentWorkflowRun (Agent 2 DomainAnalysis).
/// </summary>
public class MatchCandidateDto
{
    /// <summary>The carrier agency ID.</summary>
    [Required]
    public Guid? AgencyId { get; set; }

    /// <summary>1-based evaluated rank among candidates.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Rank must be at least 1.")]
    public int Rank { get; set; } = 1;

    /// <summary>Whether the carrier passed capacity and compliance checks.</summary>
    public bool Eligible { get; set; } = true;

    /// <summary>Calculated eligibility score between 0 and 100.</summary>
    [Range(0, 100, ErrorMessage = "EligibilityScore must be between 0 and 100.")]
    public decimal EligibilityScore { get; set; } = 100m;

    /// <summary>Rejection reason if ineligible (required when Eligible is false).</summary>
    public string? RejectionReason { get; set; }
}
