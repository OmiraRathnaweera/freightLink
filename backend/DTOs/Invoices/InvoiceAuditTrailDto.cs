namespace FreightLink.Api.DTOs.Invoices;

public class InvoiceAuditTrailDto
{
    public Guid? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? UpdatedBy { get; set; }
    public string? UpdatedByName { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidedByName { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
