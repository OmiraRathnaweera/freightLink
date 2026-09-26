using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Disputes;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Dispute management endpoints: raise, retrieve, update, and lifecycle state machine transitions (review/resolve).
/// </summary>
[ApiController]
[Route("api/v1/disputes")]
[Route("api/disputes")]
[Authorize]
public class DisputesController : ControllerBase
{
    // Business Rule 1: Created exclusively by a Shipper or an Agency
    private const string DisputeRaiseRoles = nameof(UserRole.Shipper) + "," + nameof(UserRole.AgencyStaff);
    private const string AdminRole = nameof(UserRole.Admin);

    private readonly IDisputeService _disputeService;

    /// <summary>Initializes a new instance of <see cref="DisputesController"/>.</summary>
    public DisputesController(IDisputeService disputeService)
    {
        _disputeService = disputeService;
    }

    /// <summary>Raises a new dispute for a trip in initial status 'Raised'.</summary>
    /// <param name="request">Dispute creation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 Created with the created dispute.</returns>
    [HttpPost]
    [Authorize(Roles = DisputeRaiseRoles)]
    public async Task<ActionResult<DisputeResponseDto>> Create([FromBody] CreateDisputeDto request, CancellationToken cancellationToken)
    {
        var result = await _disputeService.CreateAsync(GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Fetches a single dispute by its ID.</summary>
    /// <param name="id">The dispute's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the dispute details.</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DisputeResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _disputeService.GetByIdAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Searches, filters, and paginates disputes based on user role and query parameters.</summary>
    /// <param name="query">Filter and pagination query options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with a paged list of dispute summaries.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedDisputeResponseDto>> GetList([FromQuery] DisputeListQueryDto query, CancellationToken cancellationToken)
    {
        var result = await _disputeService.GetListAsync(query, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates an open dispute's category and description.</summary>
    /// <param name="id">The dispute's ID.</param>
    /// <param name="request">The updated dispute values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the updated dispute.</returns>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DisputeResponseDto>> Update(Guid id, [FromBody] UpdateDisputeDto request, CancellationToken cancellationToken)
    {
        var result = await _disputeService.UpdateAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Transitions a dispute from Raised to UnderReview (Admin only).</summary>
    /// <param name="id">The dispute's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the updated dispute in UnderReview status.</returns>
    [HttpPatch("{id:guid}/review")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<DisputeResponseDto>> Review(Guid id, CancellationToken cancellationToken)
    {
        var result = await _disputeService.MoveToReviewAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Resolves a dispute with mandatory resolution notes (Admin adjudication).</summary>
    /// <param name="id">The dispute's ID.</param>
    /// <param name="request">The resolution decision and notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the resolved dispute details.</returns>
    [HttpPatch("{id:guid}/resolve")]
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<DisputeResponseDto>> Resolve(Guid id, [FromBody] ResolveDisputeDto request, CancellationToken cancellationToken)
    {
        var result = await _disputeService.ResolveAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Unified status transition endpoint according to lifecycle rules (Admin only).</summary>
    /// <param name="id">The dispute's ID.</param>
    /// <param name="request">Target status and optional resolution payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the transitioned dispute.</returns>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = AdminRole)]
    public async Task<ActionResult<DisputeResponseDto>> TransitionStatus(Guid id, [FromBody] TransitionDisputeStatusDto request, CancellationToken cancellationToken)
    {
        var result = await _disputeService.TransitionStatusAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
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
        if (!Enum.TryParse<UserRole>(roleClaim, true, out var role))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid role.");
        }

        return role;
    }
}
