using System.ComponentModel.DataAnnotations;

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

    /// <summary>Currency code (e.g. LKR, USD).</summary>
    [Required]
    [StringLength(10, MinimumLength = 3)]
    public string Currency { get; set; } = "LKR";

    /// <summary>Optional due date for the invoice.</summary>
    public DateOnly? DueDate { get; set; }
}
