using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Row item DTO for paginated invoice listings.
/// </summary>
public class InvoiceListItemDto
{
    /// <summary>The unique ID of the invoice.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Alias for InvoiceId.</summary>
    public Guid Id => InvoiceId;

    /// <summary>The associated Trip ID, if any.</summary>
    public Guid? TripId { get; set; }

    /// <summary>Alias for TripId / linked entity.</summary>
    public Guid? LinkedEntityId => TripId;

    /// <summary>The unique human-readable invoice number.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Recipient user ID.</summary>
    public Guid? RecipientId { get; set; }

    /// <summary>Recipient full name if available.</summary>
    public string? RecipientName { get; set; }

    /// <summary>Recipient user role.</summary>
    public UserRole? RecipientRole { get; set; }

    /// <summary>Subtotal of line items.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Taxes total.</summary>
    public decimal TaxTotal { get; set; }

    /// <summary>Discounts total.</summary>
    public decimal DiscountTotal { get; set; }

    /// <summary>Final invoiced total amount.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>The invoiced monetary amount (backward-compatible alias for TotalAmount).</summary>
    public decimal Amount
    {
        get => TotalAmount;
        set => TotalAmount = value;
    }

    /// <summary>Currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>The current status of the invoice.</summary>
    public InvoiceStatus Status { get; set; }

    /// <summary>When the invoice was issued. Null while the invoice is still a Draft.</summary>
    public DateTimeOffset? IssuedAt { get; set; }

    /// <summary>Alias for IssuedAt.</summary>
    public DateTimeOffset? IssueDate => IssuedAt;

    /// <summary>Due date for payment if specified.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>When the invoice was paid.</summary>
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Payment reference.</summary>
    public string? PaymentReference { get; set; }

    /// <summary>Creator Agent name.</summary>
    public string? CreatedByName { get; set; }

    /// <summary>Last updater name.</summary>
    public string? UpdatedByName { get; set; }

    /// <summary>Timestamp when the invoice was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp when the invoice was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
