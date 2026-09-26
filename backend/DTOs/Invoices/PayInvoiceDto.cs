namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Request DTO for paying an issued invoice.
/// </summary>
public class PayInvoiceDto
{
    /// <summary>
    /// Optional transaction reference or gateway receipt identifier.
    /// If omitted, a unique system payment reference will be generated.
    /// </summary>
    public string? PaymentReference { get; set; }

    /// <summary>
    /// Payment method used (e.g. "Card", "PayHere", "BankTransfer").
    /// </summary>
    public string? PaymentMethod { get; set; } = "Card";
}
