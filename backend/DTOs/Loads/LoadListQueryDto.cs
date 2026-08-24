using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Search/filter/sort/paginate parameters for <see cref="Services.Interfaces.ILoadService.GetListAsync"/>.
/// Bound directly from the HTTP query string via <c>[FromQuery]</c> on <c>LoadsController.GetList</c>.
/// </summary>
public class LoadListQueryDto
{
    /// <summary>1-based page number to return. Defaults to 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Number of items per page. Defaults to 20.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Field to sort by (<c>"createdAt"</c>, <c>"pickupWindowStart"</c>, or <c>"weightKg"</c>).
    /// Unrecognized values fall back to <c>"createdAt"</c>. Defaults to <c>"createdAt"</c>.
    /// </summary>
    public string? SortBy { get; set; } = "createdAt";

    /// <summary>Sort direction, <c>"asc"</c> or <c>"desc"</c>. Defaults to <c>"desc"</c>.</summary>
    public string? SortDir { get; set; } = "desc";

    /// <summary>
    /// Case-insensitive substring match against <c>CargoDescription</c>, <c>ReferenceCode</c>,
    /// <c>PickupAddress</c>, and <c>DropoffAddress</c>. Null/empty means no filtering.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>Optional exact-match status filter.</summary>
    public LoadStatus? Status { get; set; }

    /// <summary>
    /// Optional owning-shipper filter. Callers decide what value (if any) to pass — this service
    /// does not enforce that it matches the authenticated caller; that is a controller concern.
    /// </summary>
    public Guid? ShipperUserId { get; set; }

    /// <summary>Optional inclusive lower bound on <c>CreatedAt</c>.</summary>
    public DateTimeOffset? CreatedFrom { get; set; }

    /// <summary>Optional inclusive upper bound on <c>CreatedAt</c>.</summary>
    public DateTimeOffset? CreatedTo { get; set; }
}
