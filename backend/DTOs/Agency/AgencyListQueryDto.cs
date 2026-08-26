using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

public class AgencyListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDir { get; set; } = "desc";
    public string? Search { get; set; }
    public AgencyStatus? Status { get; set; }
}
