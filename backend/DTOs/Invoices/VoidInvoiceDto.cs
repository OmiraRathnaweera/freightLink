using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Invoices;

public class VoidInvoiceDto
{
    [Required(ErrorMessage = "A non-empty void reason is required.")]
    [MinLength(1, ErrorMessage = "A non-empty void reason is required.")]
    [StringLength(1000, ErrorMessage = "Void reason cannot exceed 1000 characters.")]
    public string VoidReason { get; set; } = string.Empty;
}
