using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

public class ComplianceDocResponseDto
{
    public Guid ComplianceDocId { get; set; }
    public ComplianceDocType DocType { get; set; }
    public string DocNumber { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public ComplianceDocStatus Status { get; set; }
}
