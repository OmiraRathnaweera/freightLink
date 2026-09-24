using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Assignments;

/// <summary>
/// Query parameters for GET /api/v1/assignments.
/// </summary>
public class AssignmentListQueryDto
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>1-based page index (defaults to 1).</summary>
    [Range(1, int.MaxValue)]
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Page size between 1 and 100 (defaults to 20).</summary>
    [Range(1, 100)]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > 100 ? 20 : value;
    }

    /// <summary>Optional status filter (e.g. Proposed, Accepted, Declined).</summary>
    public AssignmentStatus? Status { get; set; }

    /// <summary>Optional search string matched against cargo description or addresses.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>Optional filter for assignments that already have (true) or do not have (false) an associated trip.</summary>
    public bool? HasTrip { get; set; }

    /// <summary>Sort column (defaults to "createdAt").</summary>
    public string SortBy { get; set; } = "createdAt";

    /// <summary>Sort direction ("asc" or "desc", defaults to "desc").</summary>
    public string SortDir { get; set; } = "desc";
}
