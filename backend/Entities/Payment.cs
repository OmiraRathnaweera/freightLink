using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Payment
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public string? GatewayRef { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public int AttemptNo { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
