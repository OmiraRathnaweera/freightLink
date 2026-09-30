using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Request body for POST /api/invoices and POST /api/v1/invoices.
/// Supports creating an invoice in Draft or Issued status with manual line items.
/// </summary>
public class CreateInvoiceDto
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

    /// <summary>Optional recipient role (Shipper, AgencyStaff, Driver, etc.).</summary>
    public UserRole? RecipientRole { get; set; }

    /// <summary>List of line items for this invoice.</summary>
    public List<InvoiceLineItemDto>? LineItems { get; set; }

    /// <summary>Monetary total amount. If line items are provided, this will be dynamically calculated from line items.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Discount amount to deduct from subtotal + taxes.</summary>
    [Range(0, 100_000_000, ErrorMessage = "DiscountTotal cannot be negative.")]
    public decimal DiscountTotal { get; set; } = 0m;

    /// <summary>ISO-4217 currency code (e.g. LKR). Defaults to LKR.</summary>
    [RegularExpression(InvoicePatterns.CurrencyCodePattern,
        ErrorMessage = "Currency must be a valid ISO-4217 code: exactly three uppercase letters (e.g. LKR, USD).")]
    public string Currency { get; set; } = "LKR";

    /// <summary>Optional due date for the invoice.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Optional notes or instructions for the invoice.</summary>
    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    public string? Notes { get; set; }

    /// <summary>Target initial status (Draft or Issued). If not specified, inferred from IssueImmediately.</summary>
    public InvoiceStatus? Status { get; set; }

    /// <summary>If true, creates the invoice directly in 'Issued' status instead of 'Draft'.</summary>
    public bool IssueImmediately { get; set; } = false;
}
