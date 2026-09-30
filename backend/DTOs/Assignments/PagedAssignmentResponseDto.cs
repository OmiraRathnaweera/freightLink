using FreightLink.Api.DTOs.Common;

namespace FreightLink.Api.DTOs.Assignments;

/// <summary>
/// Paginated envelope for assignments list (GET /api/v1/assignments).
/// </summary>
public class PagedAssignmentResponseDto : PagedResponseDto<AssignmentListItemDto>
{
}
