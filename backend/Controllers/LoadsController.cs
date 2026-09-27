using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Assignments;
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

    private const string ShipperOrAgencyStaffOrAdminRoles = nameof(UserRole.Shipper) + "," + nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Admin);

    private readonly ILoadService _loadService;
    private readonly IAssignmentService _assignmentService;
    private readonly IPricingEstimatorService _pricingEstimatorService;

    /// <summary>Creates the controller with its injected services.</summary>
    public LoadsController(ILoadService loadService, IAssignmentService assignmentService, IPricingEstimatorService pricingEstimatorService)
    {
        _loadService = loadService;
        _assignmentService = assignmentService;
        _pricingEstimatorService = pricingEstimatorService;
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
    /// Fetches a single load. A Shipper may only fetch a load they own; an Admin may fetch any load;
    /// an Agency may fetch available marketplace loads (Posted) or loads assigned to their agency.
    /// </summary>
    /// <param name="id">The load's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the matching <see cref="LoadResponseDto"/>.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = ShipperOrAgencyStaffOrAdminRoles)]
    public async Task<ActionResult<LoadResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _loadService.GetByIdAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Searches, filters, sorts, and paginates loads. A Shipper sees their own loads; an Admin sees every load;
    /// an Agency sees open marketplace loads (Posted) or loads assigned to their agency (Requirement 2 & 5).
    /// </summary>
    /// <param name="query">Search/filter/sort/paging parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with a <see cref="PagedLoadResponseDto"/>.</returns>
    [HttpGet]
    [Authorize(Roles = ShipperOrAgencyStaffOrAdminRoles)]
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

    /// <summary>
    /// Computes a rough, pre-matching price quote for the load using the straight-line (haversine)
    /// distance between its own pickup/dropoff coordinates — no external routing call, no agency/vehicle
    /// chosen yet. Accessible only by the owning Shipper. This is a preview only: it does not persist
    /// anything onto the load, and is distinct from the AI agent's own <c>Load.EstimatedPrice</c>.
    /// </summary>
    /// <param name="id">The load id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the <see cref="LoadPriceEstimateResponseDto"/>.</returns>
    [HttpPost("{id:guid}/estimate")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<LoadPriceEstimateResponseDto>> EstimatePrice(Guid id, CancellationToken cancellationToken)
    {
        var result = await _pricingEstimatorService.EstimateForShipperAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the match recommendation, candidates, validation checks, and workflow steps for a load.
    /// Accessible only by the Shipper who owns the load. Admins oversee agencies and pricing but
    /// must not view, rerun, approve, reject, or revise a shipper's AI match decision.
    /// </summary>
    /// <param name="loadId">The load id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the <see cref="LoadMatchRecommendationDto"/>.</returns>
    [HttpGet("{loadId:guid}/match")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<LoadMatchRecommendationDto>> GetMatchRecommendation(
        Guid loadId,
        [FromQuery] bool rerun = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _assignmentService.GetMatchRecommendationAsync(
            loadId,
            GetCurrentUserId(),
            GetCurrentUserRole(),
            rerun,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Confirms a matched agency proposal for a load (concurrency-safe, ADR-013 / ADR-016).
    /// Creates an Assignment in Proposed status, records ApprovalDecision, sends agency proposal email,
    /// and completes the workflow run. Only the owning Shipper may take this decision.
    /// </summary>
    /// <param name="loadId">The load id.</param>
    /// <param name="request">The agency chosen by the shipper.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the created <see cref="AssignmentResponseDto"/>.</returns>
    [HttpPost("{loadId:guid}/match/confirm")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<AssignmentResponseDto>> ConfirmMatch(
        Guid loadId,
        [FromBody] ConfirmMatchDto request,
        CancellationToken cancellationToken)
    {
        var result = await _assignmentService.ConfirmMatchAsync(
            loadId,
            request,
            GetCurrentUserId(),
            GetCurrentUserRole(),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Rejects the load's current match recommendation. Records a Reject approval decision
    /// with the shipper's reason and aborts the workflow run. Only the owning Shipper may take this decision.
    /// </summary>
    /// <param name="loadId">The load id.</param>
    /// <param name="request">The shipper's reason for rejecting the recommendation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the <see cref="MatchDecisionResponseDto"/>.</returns>
    [HttpPost("{loadId:guid}/match/reject")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<MatchDecisionResponseDto>> RejectMatch(
        Guid loadId,
        [FromBody] MatchDecisionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _assignmentService.RejectMatchAsync(
            loadId,
            request,
            GetCurrentUserId(),
            GetCurrentUserRole(),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Requests a revised match recommendation for the load. Records a Revise approval decision
    /// with the shipper's reason and aborts the workflow run so a fresh recommendation can be fetched.
    /// Only the owning Shipper may take this decision.
    /// </summary>
    /// <param name="loadId">The load id.</param>
    /// <param name="request">The shipper's reason for requesting a revised recommendation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the <see cref="MatchDecisionResponseDto"/>.</returns>
    [HttpPost("{loadId:guid}/match/revise")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<MatchDecisionResponseDto>> ReviseMatch(
        Guid loadId,
        [FromBody] MatchDecisionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _assignmentService.ReviseMatchAsync(
            loadId,
            request,
            GetCurrentUserId(),
            GetCurrentUserRole(),
            cancellationToken);

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
