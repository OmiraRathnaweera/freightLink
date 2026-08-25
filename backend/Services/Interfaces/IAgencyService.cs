using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Provides operations for managing agencies and their lifecycles.
/// </summary>
public interface IAgencyService
{
    /// <summary>
    /// Creates a new agency in a pending status.
    /// </summary>
    Task<AgencyResponseDto> CreateAsync(Guid currentUserId, UserRole currentUserRole, AgencyCreateDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an agency by its ID.
    /// </summary>
    Task<AgencyResponseDto> GetByIdAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged list of all agencies matching the query.
    /// </summary>
    Task<PagedAgencyResponseDto> GetListAsync(Guid currentUserId, UserRole currentUserRole, AgencyListQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agency's details.
    /// </summary>
    Task<AgencyResponseDto> UpdateAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, AgencyUpdateDto request, CancellationToken cancellationToken = default);
}
