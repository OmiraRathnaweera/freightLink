using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Payload for <c>POST /api/v1/loads/{loadId}/match/reject</c> and <c>POST /api/v1/loads/{loadId}/match/revise</c>.
/// Sent by the authenticated Shipper to decline the current AI recommendation and record why.
/// </summary>
public class MatchDecisionRequestDto
{
    /// <summary>
    /// The Shipper's reason for rejecting or requesting a revised recommendation.
    /// Persisted on the append-only <c>ApprovalDecision</c> row; the database's
    /// <c>ck_ad_reason</c> constraint requires a non-null reason for any non-Approve decision.
    /// </summary>
    [Required(ErrorMessage = "Reason is required.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 1000 characters.")]
    public string Reason { get; set; } = string.Empty;
}
