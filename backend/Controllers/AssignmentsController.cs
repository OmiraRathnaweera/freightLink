using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Endpoints for managing assignments / job proposals (Component C, Section 4.4).
/// Allows Agency Staff to view incoming proposals and respond (accept/decline).
/// </summary>
[ApiController]
[Route("api/v1/assignments")]
[Authorize]
public class AssignmentsController : ControllerBase
{
    private const string AgencyStaffRole = nameof(UserRole.AgencyStaff);
    private const string AgencyStaffOrAdminRoles = nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Admin);
    private const string AnyAssignmentRole = nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Shipper) + "," + nameof(UserRole.Admin);

    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    /// <summary>
    /// Lists assignments for the caller's agency (job proposal inbox).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AgencyStaffOrAdminRoles)]
    public async Task<ActionResult<PagedAssignmentResponseDto>> GetList([FromQuery] AssignmentListQueryDto query, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.GetListAsync(query, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single assignment's detail including load and pricing information.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = AnyAssignmentRole)]
    public async Task<ActionResult<AssignmentResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.GetByIdAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Accepts a proposed load assignment, creating the real Assignment (Accepted)
    /// and creating/updating the Trip to Assigned status (ADR-017 / Y3S01-143).
    /// </summary>
    [HttpPost("{loadId:guid}/accept")]
    [Authorize(Roles = AgencyStaffRole)]
    public async Task<ActionResult<AssignmentResponseDto>> Accept(Guid loadId, [FromBody] ApproveAssignmentDto? request, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.AcceptAsync(loadId, request, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Declines a proposed load assignment (triggers auto-retry notification per ADR-018).
    /// </summary>
    [HttpPost("{loadId:guid}/decline")]
    [Authorize(Roles = AgencyStaffRole)]
    public async Task<ActionResult<AssignmentResponseDto>> Decline(Guid loadId, [FromBody] DeclineAssignmentDto? request, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.DeclineAsync(loadId, request, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Legacy accept alias for Agency Staff. Admins must not finalize a proposed agency match;
    /// the Agency accepts/declines its own proposal through the dedicated endpoints above.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = AgencyStaffRole)]
    public async Task<ActionResult<AssignmentResponseDto>> Approve(Guid id, [FromBody] ApproveAssignmentDto? request, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.ApproveAsync(id, request, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
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
        if (string.IsNullOrEmpty(roleClaim) || !Enum.TryParse<UserRole>(roleClaim, out var role))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid user role.");
        }
        return role;
    }
}
