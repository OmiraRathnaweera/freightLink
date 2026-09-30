using FreightLink.Api.DTOs.Common;

namespace FreightLink.Api.DTOs.Trips;

/// <summary>Paged response for <c>GET /api/v1/trips</c> — a page of <see cref="TripListItemDto"/> rows.</summary>
public class PagedTripResponseDto : PagedResponseDto<TripListItemDto>
{
}