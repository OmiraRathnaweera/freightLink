using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Request body for PUT /api/v1/invoices/{id}.
/// Only editable while invoice is in Draft status.
/// </summary>
public class UpdateInvoiceDto
{
    /// <summary>The revised monetary amount (must be strictly greater than 0).</summary>
    [Required]
    [Range(0.01, 100_000_000, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    /// <summary>ISO-4217 currency code (exactly three uppercase letters, e.g. LKR, USD). Defaults to LKR.</summary>
    [Required]
    [RegularExpression(InvoicePatterns.CurrencyCodePattern,
        ErrorMessage = "Currency must be a valid ISO-4217 code: exactly three uppercase letters (e.g. LKR, USD).")]
    public string Currency { get; set; } = "LKR";

    /// <summary>
    /// Optional due date for the invoice. When supplied, must not be earlier than the
    /// invoice's existing issuance date (UTC) — enforced by service validation and the
    /// <c>ck_invoice_due</c> database constraint.
    /// </summary>
    public DateOnly? DueDate { get; set; }
}
