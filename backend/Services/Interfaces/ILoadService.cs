using FreightLink.Api.DTOs.Loads;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Create/read/edit/cancel operations on <see cref="Entities.Load"/>. Per ADR-019, a load is never
/// hard-deleted — "cancelling" is a status transition recorded as a <see cref="Entities.LoadStatusHistory"/>
/// row, governed by <see cref="Common.Domain.LoadStatusTransitionRules"/>. This service performs no
/// authentication, authorization, or resource-ownership checks: every acting-user id is accepted as a
/// plain <see cref="Guid"/> parameter, and validating that it belongs to the correct, currently
/// authenticated caller is the responsibility of a future controller/authorization layer.
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

    /// <summary>Fetches a single load by id.</summary>
    /// <param name="loadId">The load's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching load.</returns>
    /// <exception cref="Common.Exceptions.ApiException">404 if no load with this id exists.</exception>
    Task<LoadResponseDto> GetByIdAsync(Guid loadId, CancellationToken cancellationToken = default);

    /// <summary>Searches, filters, sorts, and paginates loads.</summary>
    /// <param name="query">The search/filter/sort/paging parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching page of loads.</returns>
    Task<PagedLoadResponseDto> GetListAsync(LoadListQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a load's content fields. Does not change <see cref="Entities.Load.Status"/> — only
    /// allowed while the load is in a status <see cref="Common.Domain.LoadStatusTransitionRules.CanEdit"/> permits.
    /// </summary>
    /// <param name="loadId">The load's id.</param>
    /// <param name="request">The new content values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated load.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if no load with this id exists; 422 if the load's current status doesn't allow editing;
    /// 400 if <see cref="UpdateLoadDto.PickupWindowEnd"/> is not after <see cref="UpdateLoadDto.PickupWindowStart"/>.
    /// </exception>
    Task<LoadResponseDto> UpdateAsync(Guid loadId, UpdateLoadDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a load to <see cref="Entities.Enums.LoadStatus.Cancelled"/> and records a
    /// <see cref="Entities.LoadStatusHistory"/> row with the given reason. This is a status
    /// transition, not a delete — no <c>Load</c> row is ever removed.
    /// </summary>
    /// <param name="loadId">The load's id.</param>
    /// <param name="cancelledByUserId">The id of the user performing the cancellation.</param>
    /// <param name="request">The cancellation reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancelled load.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 404 if no load with this id exists; 422 if the load's current status doesn't allow
    /// cancellation; 400 if <see cref="CancelLoadDto.Reason"/> is missing.
    /// </exception>
    Task<LoadResponseDto> CancelAsync(Guid loadId, Guid cancelledByUserId, CancelLoadDto request, CancellationToken cancellationToken = default);
}
