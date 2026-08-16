using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Request body for POST /api/v1/invoices.
/// </summary>
public class CreateInvoiceDto
{
    /// <summary>The ID of the trip this invoice is generated for.</summary>
    [Required]
    public Guid TripId { get; set; }

    /// <summary>The monetary amount for the invoice (must be strictly greater than 0).</summary>
    [Required]
    [Range(0.01, 100_000_000, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    /// <summary>Currency code (e.g. LKR, USD). Defaults to LKR.</summary>
    [Required]
    [StringLength(10, MinimumLength = 3)]
    public string Currency { get; set; } = "LKR";

    /// <summary>Optional due date for the invoice.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>If true, creates the invoice directly in 'Issued' status instead of 'Draft'.</summary>
    public bool IssueImmediately { get; set; } = false;
}
