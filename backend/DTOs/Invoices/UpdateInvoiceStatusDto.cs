using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Request body for PATCH /api/v1/invoices/{id}/status.
/// </summary>
public class UpdateInvoiceStatusDto
{
    /// <summary>The target status to transition the invoice to.</summary>
    [Required]
    public InvoiceStatus Status { get; set; }
}
