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

    /// <summary>
    /// Approves a proposed assignment or load, creating the real Assignment (Accepted)
    /// and updating/creating the Trip to Assigned status (ADR-016 / Y3S01-96).
    /// Records an ApprovalDecision row (Approve) and marks the workflow run as Completed.
    /// </summary>
    Task<AssignmentResponseDto> ApproveAsync(Guid assignmentOrLoadId, ApproveAssignmentDto? request, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a proposed assignment or load, creating the real Assignment (Accepted),
    /// recording an AssignmentResponse (Accepted), and creating the Trip in Assigned status (ADR-017 / Y3S01-143).
    /// </summary>
    Task<AssignmentResponseDto> AcceptAsync(Guid assignmentOrLoadId, ApproveAssignmentDto? request, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves an AI agent workflow run from the admin console (Y3S01-95/96),
    /// creating the real Assignment (Accepted) and creating/updating the Trip to Assigned status.
    /// </summary>
    Task<AssignmentResponseDto> ApproveWorkflowRunAsync(Guid workflowRunId, ApproveWorkflowRunDto? request, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a matched agency proposal for a load by the shipper (concurrency-safe, ADR-013 / ADR-016).
    /// Creates an Assignment in Proposed status, records ApprovalDecision, sends agency proposal email,
    /// and completes the workflow run.
    /// </summary>
    Task<AssignmentResponseDto> ConfirmMatchAsync(Guid loadId, FreightLink.Api.DTOs.Loads.ConfirmMatchDto request, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the AI workflow match recommendation, candidates, validation, and steps for a load.
    /// </summary>
    Task<FreightLink.Api.DTOs.Loads.LoadMatchRecommendationDto> GetMatchRecommendationAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, bool rerun = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects the load's current match recommendation. Records a Reject <c>ApprovalDecision</c> with the
    /// Shipper's reason and aborts the workflow run; a new recommendation must be requested afterwards.
    /// </summary>
    Task<FreightLink.Api.DTOs.Loads.MatchDecisionResponseDto> RejectMatchAsync(Guid loadId, FreightLink.Api.DTOs.Loads.MatchDecisionRequestDto request, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a revised match recommendation for the load. Records a Revise <c>ApprovalDecision</c> with the
    /// Shipper's reason and aborts the workflow run so a fresh recommendation can be fetched.
    /// </summary>
    Task<FreightLink.Api.DTOs.Loads.MatchDecisionResponseDto> ReviseMatchAsync(Guid loadId, FreightLink.Api.DTOs.Loads.MatchDecisionRequestDto request, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);
}
