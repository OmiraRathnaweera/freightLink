using FreightLink.Api.DTOs.Common;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Paginated list response of <see cref="DisputeListItemDto"/> items.
/// </summary>
public class PagedDisputeResponseDto : PagedResponseDto<DisputeListItemDto>
{
}
