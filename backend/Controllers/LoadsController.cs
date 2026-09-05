using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Load management endpoints (Component A): create, read (single/list), edit, and change status
/// (publish/cancel). Deliberately thin — every action just extracts the caller's identity/role from
/// the access token and delegates to <see cref="ILoadService"/>, which owns all business rules
/// including ownership enforcement.
/// </summary>
[ApiController]
[Route("api/v1/loads")]
[Authorize]
public class LoadsController : ControllerBase
{
    /// <summary>
    /// <see cref="Authorize"/>'s <c>Roles</c> property must be a compile-time constant, so it can't
    /// take a <see cref="UserRole"/> value directly — these use <see langword="nameof"/> instead of
    /// string literals so a renamed enum member fails to compile here rather than silently
    /// desyncing from the actual role name.
    /// </summary>
    private const string ShipperRole = nameof(UserRole.Shipper);

    /// <summary>See <see cref="ShipperRole"/>.</summary>
    private const string ShipperOrAdminRoles = nameof(UserRole.Shipper) + "," + nameof(UserRole.Admin);

    private readonly ILoadService _loadService;

    /// <summary>Creates the controller with its injected load service.</summary>
    public LoadsController(ILoadService loadService)
    {
        _loadService = loadService;
    }

    /// <summary>Creates a new load owned by the authenticated Shipper.</summary>
    /// <param name="request">The load's content and initial-status instruction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the created <see cref="LoadResponseDto"/>.</returns>
    [HttpPost]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<LoadResponseDto>> Create([FromBody] CreateLoadDto request, CancellationToken cancellationToken)
    {
        var result = await _loadService.CreateAsync(GetCurrentUserId(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Fetches a single load. A Shipper may only fetch a load they own; an Admin may fetch any load.
    /// </summary>
    /// <param name="id">The load's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the matching <see cref="LoadResponseDto"/>.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = ShipperOrAdminRoles)]
    public async Task<ActionResult<LoadResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _loadService.GetByIdAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Searches, filters, sorts, and paginates loads. A Shipper always sees only their own loads,
    /// regardless of any <c>shipperUserId</c> filter supplied; an Admin sees every load.
    /// </summary>
    /// <param name="query">Search/filter/sort/paging parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with a <see cref="PagedLoadResponseDto"/>.</returns>
    [HttpGet]
    [Authorize(Roles = ShipperOrAdminRoles)]
    public async Task<ActionResult<PagedLoadResponseDto>> GetList([FromQuery] LoadListQueryDto query, CancellationToken cancellationToken)
    {
        var result = await _loadService.GetListAsync(query, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Edits a load's content. Only the owning Shipper may edit, and only while Draft/Posted.</summary>
    /// <param name="id">The load's id.</param>
    /// <param name="request">The new content values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the updated <see cref="LoadResponseDto"/>.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<LoadResponseDto>> Update(Guid id, [FromBody] UpdateLoadDto request, CancellationToken cancellationToken)
    {
        var result = await _loadService.UpdateAsync(id, GetCurrentUserId(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Changes a load's status — the single endpoint for every Shipper-initiated status change
    /// (publishing a Draft load to Posted, or cancelling). This is a status transition, never a hard
    /// delete. Only the owning Shipper may change status, and only into a status/from a status this
    /// endpoint permits.
    /// </summary>
    /// <param name="id">The load's id.</param>
    /// <param name="request">The target status and (when cancelling) the reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the <see cref="LoadResponseDto"/> in its new status.</returns>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<LoadResponseDto>> ChangeStatus(Guid id, [FromBody] ChangeLoadStatusDto request, CancellationToken cancellationToken)
    {
        var result = await _loadService.ChangeStatusAsync(id, GetCurrentUserId(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Extracts the authenticated user's id from the <c>NameIdentifier</c> claim on the access token.</summary>
    /// <returns>The caller's user id.</returns>
    /// <exception cref="ApiException">401 if the claim is absent or not a well-formed GUID.</exception>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid user id.");
        }

        return userId;
    }

    /// <summary>Extracts the authenticated user's role from the <c>Role</c> claim on the access token.</summary>
    /// <returns>The caller's role.</returns>
    /// <exception cref="ApiException">401 if the claim is absent or not a recognized role.</exception>
    private UserRole GetCurrentUserRole()
    {
        var roleClaim = User.FindFirstValue(ClaimTypes.Role);

        // Enum.TryParse alone accepts any string that looks like an underlying-type literal (e.g.
        // "99") even when no UserRole member has that value — it only rejects strings that can't
        // parse as *some* integer/name at all. Enum.IsDefined closes that gap so an
        // out-of-range/malformed role claim is rejected here rather than reaching LoadService's
        // Admin-vs-not-Admin authorization checks as a technically-valid-looking but meaningless role.
        if (!Enum.TryParse<UserRole>(roleClaim, out var role) || !Enum.IsDefined(role))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid role.");
        }

        return role;
    }
}
