using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Payload for <c>DELETE /api/v1/loads/{id}</c> — cancels a load. Not <c>[Required]</c>: the DB only
/// requires a reason on the resulting <c>LoadStatusHistory</c> row when <c>ToStatus == Cancelled</c>
/// (<c>ck_lsh_cancel_reason</c>), which the service layer enforces at that point, not here.
/// </summary>
public class CancelLoadDto
{
    /// <summary>Optional free-text reason for the cancellation.</summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}
