using FreightLink.Api.DTOs.Common;

namespace FreightLink.Api.DTOs.Loads;

/// <summary>Paged response for <c>GET /api/v1/loads</c> — a page of <see cref="LoadListItemDto"/> rows.</summary>
public class PagedLoadResponseDto : PagedResponseDto<LoadListItemDto>
{
}
