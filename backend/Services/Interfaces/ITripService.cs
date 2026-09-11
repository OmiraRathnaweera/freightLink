using FreightLink.Api.DTOs.Trips;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Read, status-advance, and evidence-capture operations on <see cref="Entities.Trip"/>. Mirrors
/// <see cref="ILoadService"/>'s shape: this service performs no authentication — every acting-user
/// id/role is accepted as a plain parameter, sourced by the caller (<c>TripsController</c>) from
/// validated JWT claims, never from a request body.
///
/// <para>
/// <b>Ownership is more layered here than on <see cref="ILoadService"/></b>, since a trip has four
/// distinct "own" relationships instead of one. The (not-yet-implemented) concrete class must resolve:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="UserRole.AgencyStaff"/> — own agency, via <c>Trip.Assignment.AgencyId == AgencyStaff.AgencyId</c> for the caller's <c>AgencyStaff</c> row.</description></item>
/// <item><description><see cref="UserRole.Driver"/> — own trips only, via <c>Trip.DriverId</c> matching the caller's own <c>Driver</c> row (looked up by <c>Driver.UserId == currentUserId</c>), not by role alone.</description></item>
/// <item><description><see cref="UserRole.Shipper"/> — own load only, via <c>Trip.Assignment.Load.ShipperUserId == currentUserId</c>. Per the API contract, Shipper only has access to <see cref="GetByIdAsync"/> (trip detail), not <see cref="GetListAsync"/> (the trips list/dashboard) or the write endpoints.</description></item>
/// <item><description><see cref="UserRole.Admin"/> — every trip, no ownership check.</description></item>
/// </list>
/// </summary>
public interface ITripService
{
    /// <summary>
    /// Searches, filters, sorts, and paginates trips. Per the API contract, this is an
    /// AgencyStaff/Driver/Admin operation — Shipper is not a valid caller here (see
    /// <see cref="GetByIdAsync"/> for the Shipper-facing counterpart). An
    /// <see cref="UserRole.AgencyStaff"/> caller is scoped to trips belonging to their own agency; a
    /// <see cref="UserRole.Driver"/> caller is scoped to their own assigned trips; an
    /// <see cref="UserRole.Admin"/> caller sees every trip.
    /// </summary>
    /// <param name="query">The search/filter/sort/paging parameters.</param>
    /// <param name="currentUserId">The authenticated caller's id.</param>
    /// <param name="currentUserRole">The authenticated caller's role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching page of trips.</returns>
    Task<PagedTripResponseDto> GetListAsync(TripListQueryDto query, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single trip's full detail, including its status timeline and captured evidence.
    /// Unlike <see cref="GetListAsync"/>, a <see cref="UserRole.Shipper"/> caller may call this for a
    /// trip on their own load.
    /// </summary>
    /// <param name="tripId">The trip's id.</param>
    /// <param name="currentUserId">The authenticated caller's id.</param>
    /// <param name="currentUserRole">The authenticated caller's role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching trip, with its events and evidence.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if no trip with this id exists; 403 if the caller is not an <see cref="UserRole.Admin"/>
    /// and does not have one of the "own" relationships to this trip described on this interface's
    /// class-level remarks.
    /// </exception>
    Task<TripResponseDto> GetByIdAsync(Guid tripId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances a trip's status, recording a <see cref="Entities.TripEvent"/> row for the transition.
    /// Only <see cref="UserRole.AgencyStaff"/> (own agency) and <see cref="UserRole.Driver"/> (own
    /// trip) may call this. A transition to <see cref="TripStatus.PickedUp"/> or
    /// <see cref="TripStatus.Delivered"/> that lacks the matching <see cref="Entities.TripEvidence"/>
    /// row is rejected by the database's <c>fn_require_trip_evidence</c> trigger — this method must
    /// catch that failure and translate it into a client-facing <c>ApiException</c>, not let the raw
    /// <c>PostgresException</c> propagate.
    /// </summary>
    /// <param name="tripId">The trip's id.</param>
    /// <param name="actingUserId">The authenticated caller's id — also recorded as <c>TripEvent.RecordedByUserId</c>.</param>
    /// <param name="actingUserRole">The authenticated caller's role.</param>
    /// <param name="request">The target status and optional notes/GPS snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The trip in its new status.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if no trip with this id exists; 403 if the caller does not have an "own" relationship to
    /// this trip; 422 if the requested transition is not legal from the trip's current status, or the
    /// required evidence for a <c>PickedUp</c>/<c>Delivered</c> transition is missing.
    /// </exception>
    Task<TripResponseDto> ChangeStatusAsync(Guid tripId, Guid actingUserId, UserRole actingUserRole, ChangeTripStatusDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures proof-of-pickup or proof-of-delivery for a trip by linking an already-uploaded file.
    /// Per the API contract's role matrix, <see cref="UserRole.AgencyStaff"/> may only submit
    /// <see cref="EvidenceType.PickupProof"/> and <see cref="UserRole.Driver"/> may only submit
    /// <see cref="EvidenceType.DeliveryProof"/> — this method must enforce that pairing, not just
    /// role membership in isolation. A trip may have at most one row per <see cref="EvidenceType"/>
    /// (<c>uq_tripevidence_type</c>); a duplicate submission must be rejected, not silently overwritten,
    /// since <c>TripEvidence</c> is append-only at the database level.
    /// </summary>
    /// <param name="tripId">The trip's id.</param>
    /// <param name="actingUserId">The authenticated caller's id — recorded as <c>TripEvidence.CapturedByUserId</c>.</param>
    /// <param name="actingUserRole">The authenticated caller's role.</param>
    /// <param name="request">The uploaded file's public id, evidence type, and optional GPS coordinates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created evidence row.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if no trip with this id exists; 403 if the caller does not have an "own" relationship to
    /// this trip, or the caller's role doesn't match the requested <see cref="UploadTripEvidenceDto.EvidenceType"/>;
    /// 409 if evidence of this type already exists for this trip.
    /// </exception>
    Task<TripEvidenceResponseDto> UploadEvidenceAsync(Guid tripId, Guid actingUserId, UserRole actingUserRole, UploadTripEvidenceDto request, CancellationToken cancellationToken = default);

    /// <summary>Lists all captured evidence for a trip (at most two rows: one <c>PickupProof</c>, one <c>DeliveryProof</c>).</summary>
    /// <param name="tripId">The trip's id.</param>
    /// <param name="currentUserId">The authenticated caller's id.</param>
    /// <param name="currentUserRole">The authenticated caller's role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The trip's evidence rows.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if no trip with this id exists; 403 if the caller is not an <see cref="UserRole.Admin"/>
    /// and does not have one of the "own" relationships to this trip described on this interface's
    /// class-level remarks.
    /// </exception>
    Task<List<TripEvidenceResponseDto>> GetEvidenceAsync(Guid tripId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);
}