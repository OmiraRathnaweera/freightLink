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
    /// Retrieves every agency in <c>Pending</c> status together with the compliance documents it has
    /// uploaded so far, for the admin verification queue.
    /// </summary>
    Task<IEnumerable<AgencyVerificationQueueItemDto>> GetVerificationQueueAsync(CancellationToken cancellationToken = default);

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

    Task<ComplianceDocResponseDto> AddComplianceDocAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, ComplianceDocCreateDto request, CancellationToken cancellationToken = default);
    Task<IEnumerable<ComplianceDocResponseDto>> GetComplianceDocsAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing compliance document's file/number/dates in place and resets its
    /// <c>Status</c> to <c>Pending</c> so it is re-verified, rather than inserting a new row — a plain
    /// re-<see cref="AddComplianceDocAsync"/> would collide with the <c>ux_compliancedoc_live</c>
    /// partial unique index while the existing document is still <c>Pending</c>/<c>Verified</c>.
    /// </summary>
    Task<ComplianceDocResponseDto> UpdateComplianceDocAsync(Guid agencyId, Guid complianceDocId, Guid currentUserId, UserRole currentUserRole, ComplianceDocUpdateDto request, CancellationToken cancellationToken = default);

    /// <summary>Admin-only: marks a Pending compliance document as Verified.</summary>
    Task<ComplianceDocResponseDto> VerifyComplianceDocAsync(Guid agencyId, Guid complianceDocId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>Admin-only: marks a Pending compliance document as Rejected.</summary>
    Task<ComplianceDocResponseDto> RejectComplianceDocAsync(Guid agencyId, Guid complianceDocId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    Task<VehicleResponseDto> AddVehicleAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, VehicleCreateDto request, CancellationToken cancellationToken = default);
    Task<IEnumerable<VehicleResponseDto>> GetVehiclesAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);
}
