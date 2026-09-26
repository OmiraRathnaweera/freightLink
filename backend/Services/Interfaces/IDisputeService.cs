using FreightLink.Api.DTOs.Disputes;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Service interface for Dispute raising, querying, editing, and state machine lifecycle transitions.
/// </summary>
public interface IDisputeService
{
    /// <summary>Raises a new dispute against a trip in initial status 'Raised'. Exclusively by Shipper or Agency.</summary>
    Task<DisputeResponseDto> CreateAsync(Guid currentUserId, UserRole role, CreateDisputeDto request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a dispute by its unique ID with role/ownership enforcement.</summary>
    Task<DisputeResponseDto> GetByIdAsync(Guid disputeId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a paginated list of disputes filtered by role, status, category, and trip.</summary>
    Task<PagedDisputeResponseDto> GetListAsync(DisputeListQueryDto query, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Updates an open dispute's category or description by the user who raised it.</summary>
    Task<DisputeResponseDto> UpdateAsync(Guid disputeId, Guid currentUserId, UserRole role, UpdateDisputeDto request, CancellationToken cancellationToken = default);

    /// <summary>Transitions a dispute from Raised to UnderReview (Admin only).</summary>
    Task<DisputeResponseDto> MoveToReviewAsync(Guid disputeId, Guid currentUserId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Resolves a dispute with formal outcome and mandatory resolution notes (Admin adjudication).</summary>
    Task<DisputeResponseDto> ResolveAsync(Guid disputeId, Guid currentUserId, UserRole role, ResolveDisputeDto request, CancellationToken cancellationToken = default);

    /// <summary>Transitions dispute status via unified endpoint according to state machine rules (Admin only).</summary>
    Task<DisputeResponseDto> TransitionStatusAsync(Guid disputeId, Guid currentUserId, UserRole role, TransitionDisputeStatusDto request, CancellationToken cancellationToken = default);
}
