using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// Search/filter/sort/paginate parameters for <see cref="Services.Interfaces.ITripService.GetListAsync"/>.
/// Bound directly from the HTTP query string via <c>[FromQuery]</c> on <c>TripsController.GetList</c>.
/// Mirrors <see cref="Loads.LoadListQueryDto"/>'s shape for consistency across list endpoints.
/// </summary>
public class TripListQueryDto
{
    /// <summary>1-based page number to return. Defaults to 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Number of items per page. Defaults to 20.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Field to sort by (<c>"createdAt"</c> or <c>"updatedAt"</c>). Unrecognized values fall back to
    /// <c>"createdAt"</c>. Defaults to <c>"createdAt"</c>.
    /// </summary>
    public string? SortBy { get; set; } = "createdAt";

    /// <summary>Sort direction, <c>"asc"</c> or <c>"desc"</c>. Defaults to <c>"desc"</c>.</summary>
    public string? SortDir { get; set; } = "desc";

    /// <summary>Optional exact-match status filter.</summary>
    public TripStatus? Status { get; set; }

    /// <summary>
    /// Optional agency filter, resolved via <c>Trip.Assignment.AgencyId</c>. Callers decide what value
    /// (if any) to pass — this service does not enforce that it matches the authenticated caller's own
    /// agency; that scoping is a controller/service ownership concern, not a query concern.
    /// </summary>
    public Guid? AgencyId { get; set; }

    /// <summary>Optional exact-match driver filter.</summary>
    public Guid? DriverId { get; set; }
}