using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Detailed response DTO for a single Invoice resource.
/// </summary>
public class InvoiceResponseDto
{
    /// <summary>The unique ID of the invoice.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Alias for InvoiceId.</summary>
    public Guid Id => InvoiceId;

    /// <summary>The unique human-readable invoice number.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>The associated Trip ID, if any.</summary>
    public Guid? TripId { get; set; }

    /// <summary>Alias for TripId / linked entity.</summary>
    public Guid? LinkedEntityId => TripId;

    /// <summary>Recipient user ID.</summary>
    public Guid? RecipientId { get; set; }

    /// <summary>Recipient user role.</summary>
    public UserRole? RecipientRole { get; set; }

    /// <summary>Recipient full name if available.</summary>
    public string? RecipientName { get; set; }

    /// <summary>Line items associated with this invoice.</summary>
    public List<InvoiceLineItemDto> LineItems { get; set; } = new();

    /// <summary>Subtotal of line items (sum of quantity * unitPrice).</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Sum of taxes on all line items.</summary>
    public decimal TaxTotal { get; set; }

    /// <summary>Discount amount deducted from subtotal + taxes.</summary>
    public decimal DiscountTotal { get; set; }

    /// <summary>Final invoiced total amount.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>The invoiced monetary amount (backward-compatible alias for TotalAmount).</summary>
    public decimal Amount
    {
        get => TotalAmount;
        set => TotalAmount = value;
    }

    /// <summary>Currency code (e.g. LKR).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>The current lifecycle status of the invoice.</summary>
    public InvoiceStatus Status { get; set; }

    /// <summary>Optional notes or terms on the invoice.</summary>
    public string? Notes { get; set; }

    /// <summary>When the invoice was issued. Null while the invoice is still a Draft.</summary>
    public DateTimeOffset? IssuedAt { get; set; }

    /// <summary>Alias for IssuedAt.</summary>
    public DateTimeOffset? IssueDate => IssuedAt;

    /// <summary>Due date for payment if specified.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>When the invoice was paid.</summary>
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Payment transaction or gateway reference.</summary>
    public string? PaymentReference { get; set; }

    /// <summary>URL of the Shipper-uploaded payment receipt, if one has been submitted.</summary>
    public string? PaymentProofUrl { get; set; }

    /// <summary>Original filename of the uploaded payment receipt, if any.</summary>
    public string? PaymentProofFileName { get; set; }

    /// <summary>When the payment receipt was uploaded, if any.</summary>
    public DateTimeOffset? PaymentProofUploadedAt { get; set; }

    /// <summary>Name of the Shipper who uploaded the payment receipt, if any.</summary>
    public string? PaymentProofUploadedByName { get; set; }

    /// <summary>Creator Agent name.</summary>
    public string? CreatedByName => AuditTrail.CreatedByName;

    /// <summary>Last updater name.</summary>
    public string? UpdatedByName => AuditTrail.UpdatedByName;

    /// <summary>Audit trail including creators, modifiers, void reasons, and timestamps.</summary>
    public InvoiceAuditTrailDto AuditTrail { get; set; } = new();

    /// <summary>Timestamp when the invoice was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp when the invoice was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
