using FreightLink.Api.DTOs.Agency;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Provides operations for managing agencies and their lifecycles.
/// </summary>
public interface IAgencyService
{
    /// <summary>
    /// Creates a new agency in a pending status.
    /// </summary>
    Task<AgencyResponseDto> CreateAsync(Guid currentUserId, AgencyCreateDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an agency by its ID.
    /// </summary>
    Task<AgencyResponseDto> GetByIdAsync(Guid agencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of all agencies.
    /// </summary>
    Task<IEnumerable<AgencyResponseDto>> GetListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agency's details.
    /// </summary>
    Task<AgencyResponseDto> UpdateAsync(Guid agencyId, AgencyUpdateDto request, CancellationToken cancellationToken = default);
}
