using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Invoices;

public class InvoiceRecipientDto
{
    public Guid RecipientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
