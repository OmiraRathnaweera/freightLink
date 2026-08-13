namespace FreightLink.Api.DTOs.Common;

/// <summary>
/// Generic paging envelope matching the API contract's standard list-response shape
/// (<c>{ items, page, pageSize, totalItems, totalPages }</c>), reusable by any endpoint that returns
/// a paginated collection.
/// </summary>
/// <typeparam name="T">The type of each item in the page.</typeparam>
public class PagedResponseDto<T>
{
    /// <summary>The items on this page.</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>The current 1-based page number.</summary>
    public int Page { get; set; }

    /// <summary>The number of items requested per page.</summary>
    public int PageSize { get; set; }

    /// <summary>The total number of items across all pages.</summary>
    public int TotalItems { get; set; }

    /// <summary>The total number of pages available.</summary>
    public int TotalPages { get; set; }
}
