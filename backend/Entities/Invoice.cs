using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Invoice
{
    public Guid InvoiceId { get; set; }
    public Guid TripId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Trip Trip { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
