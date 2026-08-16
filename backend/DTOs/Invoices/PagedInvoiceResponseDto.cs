using FreightLink.Api.DTOs.Common;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Paginated list response of <see cref="InvoiceListItemDto"/> items.
/// </summary>
public class PagedInvoiceResponseDto : PagedResponseDto<InvoiceListItemDto>
{
}
