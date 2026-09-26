using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Payload for <c>POST /api/v1/loads/{loadId}/match/confirm</c>.
/// Sent by the authenticated Shipper to choose an agency from the AI matching results.
/// </summary>
public class ConfirmMatchDto
{
    /// <summary>
    /// The ID of the agency chosen by the shipper.
    /// </summary>
    [Required(ErrorMessage = "AgencyId is required.")]
    public Guid? AgencyId { get; set; }
}
