using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Service interface for managing assignments / job proposals (Component C).
/// </summary>
public interface IAssignmentService
{
    /// <summary>
    /// Lists assignments for the caller's agency, optionally filtered by status and paginated.
    /// </summary>
    Task<PagedAssignmentResponseDto> GetListAsync(AssignmentListQueryDto query, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single assignment by its ID with full load details.
    /// </summary>
    Task<AssignmentResponseDto> GetByIdAsync(Guid assignmentId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Declines a proposed assignment for a load (ADR-017 / ADR-018).
    /// </summary>
    Task<AssignmentResponseDto> DeclineAsync(Guid loadId, DeclineAssignmentDto? request, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);
}
