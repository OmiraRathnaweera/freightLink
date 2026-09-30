using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Invoices;

/// <summary>
/// Query parameters for GET /api/invoices and GET /api/v1/invoices.
/// </summary>
public class InvoiceListQueryDto
{
    /// <summary>1-based page number. Defaults to 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Number of items per page. Defaults to 20; max 100.</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>Optional filter by invoice status.</summary>
    public InvoiceStatus? Status { get; set; }

    /// <summary>Optional filter by associated trip ID.</summary>
    public Guid? TripId { get; set; }

    /// <summary>Optional filter by recipient user ID.</summary>
    public Guid? RecipientId { get; set; }

    /// <summary>Optional filter for invoices created/issued on or after this date.</summary>
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>Optional filter for invoices created/issued on or before this date.</summary>
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>Optional text search against InvoiceNumber, recipient name, or notes.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>Optional sort direction: "asc" or "desc". Defaults to "desc".</summary>
    public string? SortOrder { get; set; } = "desc";
}
