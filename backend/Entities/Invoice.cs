using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Invoice
{
    public Guid InvoiceId { get; set; }
    public Guid? TripId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid? RecipientId { get; set; }
    public UserRole? RecipientRole { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Amount { get; set; }
    public decimal TotalAmount
    {
        get => Amount;
        set => Amount = value;
    }

    public string Currency { get; set; } = "LKR";
    public InvoiceStatus Status { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? PaymentReference { get; set; }

    // Audit trail
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Trip? Trip { get; set; }
    public User? Recipient { get; set; }
    public User? CreatedByUser { get; set; }
    public User? UpdatedByUser { get; set; }
    public User? VoidedByUser { get; set; }

    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
