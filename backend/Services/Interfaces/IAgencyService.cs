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
    /// Retrieves a list of agencies that have compliance documents expiring within the specified number of days.
    /// </summary>
    Task<IEnumerable<AgencyExpiringComplianceDto>> GetAgenciesWithExpiringComplianceAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agency's details.
    /// </summary>
    Task<AgencyResponseDto> UpdateAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, AgencyUpdateDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves/verifies a pending agency.
    /// </summary>
    Task VerifyAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a verified agency.
    /// </summary>
    Task ActivateAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends an agency.
    /// </summary>
    Task SuspendAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a vehicle to the agency's fleet.
    /// </summary>
    Task<VehicleResponseDto> AddVehicleAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, VehicleCreateDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an agency's fleet of vehicles.
    /// </summary>
    Task<IEnumerable<VehicleResponseDto>> GetVehiclesAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);
}
