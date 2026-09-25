namespace FreightLink.Api.DTOs.Agency;

public class AgencyExpiringComplianceDto
{
    public AgencyResponseDto Agency { get; set; } = null!;
    public IEnumerable<ComplianceDocResponseDto> ExpiringDocs { get; set; } = Array.Empty<ComplianceDocResponseDto>();
}
