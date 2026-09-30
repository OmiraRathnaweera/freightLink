using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Retired admin workflow-approval endpoint. The domain policy assigns AI match approval to the
/// owning Shipper and Agency Staff; an Admin must never finalize a proposed match.
/// </summary>
[ApiController]
[Route("api/v1/admin/agent-workflows")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminAgentWorkflowsController : ControllerBase
{
    /// <summary>
    /// Always rejects the legacy admin approval route. It remains temporarily so callers receive
    /// a clear authorization error rather than silently finalizing a match through an old URL.
    /// </summary>
    [HttpPost("{workflowRunId:guid}/approve")]
    public ActionResult<AssignmentResponseDto> Approve(
        Guid workflowRunId,
        [FromBody] ApproveWorkflowRunDto? request,
        CancellationToken cancellationToken)
    {
        throw new ApiException(
            HttpStatusCode.Forbidden,
            ErrorCode.FORBIDDEN,
            "Admins cannot approve AI-recommended agency matches. The owning Shipper must approve the recommendation, then the Agency Staff must accept or decline it.");
    }

}
