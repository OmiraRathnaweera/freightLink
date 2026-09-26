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
/// Admin-only endpoints for the Agent Workflow Console (Component C / Y3S01-95 / Y3S01-96).
/// Allows an Admin to review and approve AI workflow proposed matches into operational Assignments and Trips.
/// </summary>
[ApiController]
[Route("api/v1/admin/agent-workflows")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminAgentWorkflowsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AdminAgentWorkflowsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    /// <summary>
    /// Approves an AI agent workflow run proposed match from the admin console (Y3S01-95/96),
    /// finalizing the proposed candidate into an operational Assignment (Accepted) and Trip (Assigned).
    /// </summary>
    [HttpPost("{workflowRunId:guid}/approve")]
    public async Task<ActionResult<AssignmentResponseDto>> Approve(
        Guid workflowRunId,
        [FromBody] ApproveWorkflowRunDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _assignmentService.ApproveWorkflowRunAsync(
            workflowRunId,
            request,
            GetCurrentUserId(),
            GetCurrentUserRole(),
            cancellationToken);

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
