using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Disputes;

/// <summary>
/// Query parameters for GET /api/v1/disputes.
/// </summary>
public class DisputeListQueryDto
{
    /// <summary>1-based page number. Defaults to 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Number of items per page. Defaults to 20; max 100.</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>Optional filter by dispute status.</summary>
    public DisputeStatus? Status { get; set; }

    /// <summary>Optional filter by dispute category.</summary>
    public DisputeCategory? Category { get; set; }

    /// <summary>Optional filter by associated trip ID.</summary>
    public Guid? TripId { get; set; }

    /// <summary>Optional sort direction: "asc" or "desc". Defaults to "desc".</summary>
    public string? SortOrder { get; set; } = "desc";
}
