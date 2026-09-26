namespace FreightLink.Api.Entities;

public class InvoiceLineItem
{
    public Guid InvoiceLineItemId { get; set; }
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
