using FreightLink.Api.DTOs.Trips;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;

namespace FreightLink.Api.Services;

/// <summary>
/// Skeleton implementation of <see cref="ITripService"/> — exists so <see cref="Controllers.TripsController"/>
/// compiles and can be registered for DI immediately, ahead of the real business logic. Every method
/// throws <see cref="NotImplementedException"/> rather than returning fake/placeholder data, so a call
/// against a running skeleton fails loudly (500) instead of silently looking like a working feature.
/// See <see cref="ITripService"/>'s XML docs for exactly what each method must do once implemented —
/// in particular the four-way "own" resolution described in that interface's class-level remarks, and
/// the <c>fn_require_trip_evidence</c>/append-only-trigger handling called out on
/// <see cref="ChangeStatusAsync"/>/<see cref="UploadEvidenceAsync"/>.
/// </summary>
public class TripService : ITripService
{
    // TODO(Component C): inject AppDbContext (and any other dependencies the real implementation
    // needs) here once business logic is written, mirroring LoadService's constructor.

    /// <inheritdoc />
    public Task<PagedTripResponseDto> GetListAsync(TripListQueryDto query, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TripService.GetListAsync is not yet implemented — see ITripService for the required ownership scoping per role.");

    /// <inheritdoc />
    public Task<TripResponseDto> GetByIdAsync(Guid tripId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TripService.GetByIdAsync is not yet implemented — see ITripService for the required ownership scoping per role, including the Shipper (own load) case.");

    /// <inheritdoc />
    public Task<TripResponseDto> ChangeStatusAsync(Guid tripId, Guid actingUserId, UserRole actingUserRole, ChangeTripStatusDto request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TripService.ChangeStatusAsync is not yet implemented — see ITripService for the required transition validation and fn_require_trip_evidence trigger handling.");

    /// <inheritdoc />
    public Task<TripEvidenceResponseDto> UploadEvidenceAsync(Guid tripId, Guid actingUserId, UserRole actingUserRole, UploadTripEvidenceDto request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TripService.UploadEvidenceAsync is not yet implemented — see ITripService for the required role/EvidenceType pairing and duplicate-evidence rejection.");

    /// <inheritdoc />
    public Task<List<TripEvidenceResponseDto>> GetEvidenceAsync(Guid tripId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TripService.GetEvidenceAsync is not yet implemented — see ITripService for the required ownership scoping per role.");
}