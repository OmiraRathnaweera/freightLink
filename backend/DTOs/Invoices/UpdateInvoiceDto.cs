using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Request body for PUT /api/invoices/{id}.
/// Only editable while invoice is in Draft status.
/// </summary>
public class UpdateInvoiceDto
{
    /// <summary>Optional associated entity (e.g. TripId).</summary>
    public Guid? TripId { get; set; }

    /// <summary>Alias for TripId / linked entity.</summary>
    public Guid? LinkedEntityId
    {
        get => TripId;
        set => TripId = value ?? TripId;
    }

    /// <summary>Optional recipient user ID.</summary>
    public Guid? RecipientId { get; set; }

    /// <summary>Optional recipient role.</summary>
    public UserRole? RecipientRole { get; set; }

    /// <summary>Updated line items.</summary>
    public List<InvoiceLineItemDto>? LineItems { get; set; }

    /// <summary>The revised monetary amount. If line items are provided, will be recalculated.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Discount amount to deduct from subtotal + taxes.</summary>
    [Range(0, 100_000_000, ErrorMessage = "DiscountTotal cannot be negative.")]
    public decimal? DiscountTotal { get; set; }

    /// <summary>ISO-4217 currency code (e.g. LKR). Defaults to LKR.</summary>
    [RegularExpression(InvoicePatterns.CurrencyCodePattern,
        ErrorMessage = "Currency must be a valid ISO-4217 code: exactly three uppercase letters (e.g. LKR, USD).")]
    public string? Currency { get; set; }

    /// <summary>Optional due date for the invoice.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Optional notes or instructions for the invoice.</summary>
    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    public string? Notes { get; set; }
}
