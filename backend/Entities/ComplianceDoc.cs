using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class ComplianceDoc
{
    public Guid ComplianceDocId { get; set; }
    public Guid AgencyId { get; set; }
    public ComplianceDocType DocType { get; set; }
    public string DocNumber { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public ComplianceDocStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Agency Agency { get; set; } = null!;
}
