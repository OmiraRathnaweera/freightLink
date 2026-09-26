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

    /// <summary>
    /// Lists vehicles belonging to an agency.
    /// </summary>
    Task<List<VehicleResponseDto>> GetVehiclesAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists drivers belonging to an agency.
    /// </summary>
    Task<List<DriverResponseDto>> GetDriversAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a driver to an agency.
    /// </summary>
    Task<DriverResponseDto> AddDriverAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CreateDriverRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves full fleet resources (vehicles and drivers) for an agency.
    /// </summary>
    Task<AgencyFleetResponseDto> GetFleetAsync(Guid? agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds default active logistics carrier agencies across Sri Lanka (Colombo, Kandy, Galle, Kurunegala)
    /// if not already present, ensuring realistic fleet capacity for multi-agent matching.
    /// </summary>
    Task SeedDefaultAgenciesIfNotExistsAsync(CancellationToken cancellationToken = default);
}
