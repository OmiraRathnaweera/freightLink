using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Invoices;

public class InvoiceLineItemDto
{
    public Guid? InvoiceLineItemId { get; set; }

    [Required(ErrorMessage = "Line item description is required.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Description must be between 1 and 500 characters.")]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 10_000_000, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; } = 1;

    [Range(0, 100_000_000, ErrorMessage = "UnitPrice cannot be negative.")]
    public decimal UnitPrice { get; set; }

    [Range(0, 100, ErrorMessage = "TaxRate must be between 0 and 100 percent.")]
    public decimal TaxRate { get; set; } = 0;

    public decimal Amount { get; set; }
}
