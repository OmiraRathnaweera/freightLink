namespace FreightLink.Api.DTOs.Agency;

/// <summary>
/// One row of the admin verification queue (<c>GET /api/v1/agencies/verification-queue</c>): a
/// <c>Pending</c> agency paired with every compliance document it has uploaded so far, so an admin
/// can review the documents and decide whether to verify/reject each one and ultimately verify the
/// agency itself, without a separate round-trip per agency.
/// </summary>
public class AgencyVerificationQueueItemDto
{
    public AgencyResponseDto Agency { get; set; } = null!;
    public IEnumerable<ComplianceDocResponseDto> ComplianceDocs { get; set; } = Array.Empty<ComplianceDocResponseDto>();
}
