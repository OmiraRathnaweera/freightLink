using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.LoadProposals;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Manual load proposals: an Agency bids a price directly on a posted load, and the Shipper reviews
/// and accepts one — a path alongside (not through) the multi-agent matching pipeline. Deliberately
/// thin — every action delegates to <see cref="ILoadProposalService"/>, which owns all business rules.
/// </summary>
[ApiController]
[Route("api/v1/loads/{loadId:guid}/proposals")]
[Authorize]
public class LoadProposalsController : ControllerBase
{
    private const string AgencyStaffRole = nameof(UserRole.AgencyStaff);
    private const string ShipperRole = nameof(UserRole.Shipper);
    private const string AnyProposalRole = nameof(UserRole.Shipper) + "," + nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Admin);

    private readonly ILoadProposalService _loadProposalService;

    public LoadProposalsController(ILoadProposalService loadProposalService)
    {
        _loadProposalService = loadProposalService;
    }

    /// <summary>Submits a new proposal from the caller's agency on a posted load.</summary>
    /// <returns>201 with the created proposal.</returns>
    [HttpPost]
    [Authorize(Roles = AgencyStaffRole)]
    public async Task<ActionResult<LoadProposalResponseDto>> Create(Guid loadId, [FromBody] CreateLoadProposalDto request, CancellationToken cancellationToken)
    {
        var result = await _loadProposalService.CreateAsync(loadId, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Lists proposals for a load — the owning Shipper/Admin see every proposal; an Agency Staff
    /// caller sees only their own agency's.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AnyProposalRole)]
    public async Task<ActionResult<List<LoadProposalResponseDto>>> GetList(Guid loadId, CancellationToken cancellationToken)
    {
        var result = await _loadProposalService.GetListAsync(loadId, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lists every proposal across all of the caller's own loads (Shipper only) — the aggregate feed
    /// backing the Shipper's "Load Proposals" page. An absolute route override since the class-level
    /// route requires a {loadId} segment that this endpoint has no single load to scope to.
    /// </summary>
    [HttpGet("/api/v1/loads/proposals")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<List<LoadProposalResponseDto>>> GetListForShipper(CancellationToken cancellationToken)
    {
        var result = await _loadProposalService.GetListForShipperAsync(GetCurrentUserId(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Accepts a pending proposal (Shipper only): creates the Assignment, advances the load to
    /// Matched, and auto-rejects every other pending proposal on the same load.
    /// </summary>
    [HttpPost("{proposalId:guid}/accept")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<LoadProposalResponseDto>> Accept(Guid loadId, Guid proposalId, CancellationToken cancellationToken)
    {
        var result = await _loadProposalService.AcceptAsync(loadId, proposalId, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Rejects a pending proposal (Shipper only), optionally with a reason.</summary>
    [HttpPost("{proposalId:guid}/reject")]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<LoadProposalResponseDto>> Reject(Guid loadId, Guid proposalId, [FromBody] RespondLoadProposalDto? request, CancellationToken cancellationToken)
    {
        var result = await _loadProposalService.RejectAsync(loadId, proposalId, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Withdraws the caller's own pending proposal (Agency Staff only).</summary>
    [HttpPost("{proposalId:guid}/withdraw")]
    [Authorize(Roles = AgencyStaffRole)]
    public async Task<ActionResult<LoadProposalResponseDto>> Withdraw(Guid loadId, Guid proposalId, CancellationToken cancellationToken)
    {
        var result = await _loadProposalService.WithdrawAsync(loadId, proposalId, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid user id.");
        }

        return userId;
    }

    private UserRole GetCurrentUserRole()
    {
        var roleClaim = User.FindFirstValue(ClaimTypes.Role);
        if (!Enum.TryParse<UserRole>(roleClaim, out var role) || !Enum.IsDefined(role))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid role.");
        }

        return role;
    }
}
