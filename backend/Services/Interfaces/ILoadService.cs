using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Create/read/edit/cancel operations on <see cref="Entities.Load"/>. Per ADR-019, a load is never
/// hard-deleted — "cancelling" is a status transition recorded as a <see cref="Entities.LoadStatusHistory"/>
/// row, governed by <see cref="Common.Domain.LoadStatusTransitionRules"/>. This service performs no
/// authentication — every acting-user id/role is accepted as a plain parameter, sourced by the caller
/// (in practice, <c>LoadsController</c>) from validated JWT claims, never from a request body.
/// Ownership enforcement (a <c>Shipper</c> may only see/edit/cancel their own loads; an <c>Admin</c>
/// may read any load) is a business rule and so lives here, not in the controller.
/// </summary>
public interface ILoadService
{
    /// <summary>
    /// Creates a new load owned by <paramref name="shipperUserId"/>, as <c>Draft</c> or (when
    /// <see cref="CreateLoadDto.PostImmediately"/> is <c>true</c>) directly as <c>Posted</c>, and
    /// records the initial <see cref="Entities.LoadStatusHistory"/> row.
    /// </summary>
    /// <param name="shipperUserId">The id of the user the new load is created for.</param>
    /// <param name="request">The load's content and initial-status instruction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created load.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 400 if <see cref="CreateLoadDto.PickupWindowEnd"/> is not after <see cref="CreateLoadDto.PickupWindowStart"/>.
    /// </exception>
    Task<LoadResponseDto> CreateAsync(Guid shipperUserId, CreateLoadDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single load by id. An <see cref="UserRole.Admin"/> caller may fetch any load; any
    /// other role may only fetch a load it owns.
    /// </summary>
    /// <param name="loadId">The load's id.</param>
    /// <param name="currentUserId">The authenticated caller's id.</param>
    /// <param name="currentUserRole">The authenticated caller's role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching load.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if no load with this id exists; 403 if the caller is not an <see cref="UserRole.Admin"/>
    /// and does not own this load.
    /// </exception>
    Task<LoadResponseDto> GetByIdAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches, filters, sorts, and paginates loads. An <see cref="UserRole.Admin"/> caller sees
    /// every load (optionally narrowed by <see cref="LoadListQueryDto.ShipperUserId"/>); any other
    /// role is always scoped to its own loads regardless of what <see cref="LoadListQueryDto.ShipperUserId"/> requests.
    /// </summary>
    /// <param name="query">The search/filter/sort/paging parameters.</param>
    /// <param name="currentUserId">The authenticated caller's id.</param>
    /// <param name="currentUserRole">The authenticated caller's role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching page of loads.</returns>
    Task<PagedLoadResponseDto> GetListAsync(LoadListQueryDto query, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a load's content fields. Does not change <see cref="Entities.Load.Status"/> — only
    /// allowed while the load is in a status <see cref="Common.Domain.LoadStatusTransitionRules.CanEdit"/>
    /// permits, and only for the load's owner.
    /// </summary>
    /// <param name="loadId">The load's id.</param>
    /// <param name="currentUserId">The authenticated caller's id — must own the load.</param>
    /// <param name="request">The new content values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated load.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if no load with this id exists; 403 if the caller does not own this load; 422 if the
    /// load's current status doesn't allow editing; 400 if <see cref="UpdateLoadDto.PickupWindowEnd"/>
    /// is not after <see cref="UpdateLoadDto.PickupWindowStart"/>.
    /// </exception>
    Task<LoadResponseDto> UpdateAsync(Guid loadId, Guid currentUserId, UpdateLoadDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a load to <see cref="Entities.Enums.LoadStatus.Cancelled"/> and records a
    /// <see cref="Entities.LoadStatusHistory"/> row with the given reason. This is a status
    /// transition, not a delete — no <c>Load</c> row is ever removed. Only the load's owner may
    /// cancel it.
    /// </summary>
    /// <param name="loadId">The load's id.</param>
    /// <param name="cancelledByUserId">
    /// The authenticated caller's id — must own the load. Also recorded as the acting user on the
    /// resulting <see cref="Entities.LoadStatusHistory"/> row.
    /// </param>
    /// <param name="request">The cancellation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancelled load.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if no load with this id exists; 403 if the caller does not own this load; 422 if the
    /// load's current status doesn't allow cancellation; 400 if <see cref="CancelLoadDto.Reason"/>
    /// is missing.
    /// </exception>
    Task<LoadResponseDto> CancelAsync(Guid loadId, Guid cancelledByUserId, CancelLoadDto request, CancellationToken cancellationToken = default);
}
