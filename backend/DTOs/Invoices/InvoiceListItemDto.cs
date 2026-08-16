using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Row item DTO for paginated invoice listings.
/// </summary>
public class InvoiceListItemDto
{
    /// <summary>The unique ID of the invoice.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>The associated Trip ID.</summary>
    public Guid TripId { get; set; }

    /// <summary>The unique human-readable invoice number.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>The invoiced monetary amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>The current status of the invoice.</summary>
    public InvoiceStatus Status { get; set; }

    /// <summary>When the invoice was issued.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>Due date for payment if specified.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Timestamp when the invoice was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
