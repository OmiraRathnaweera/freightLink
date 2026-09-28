using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Public, token-gated endpoint for the Accept/Decline buttons in a job-proposal email.
/// Deliberately its own controller, with no <c>[Authorize]</c> and no internal-service-key
/// filter: it is neither a JWT-authenticated user endpoint nor an internal service-to-service
/// one, and living apart avoids ever accidentally inheriting either attribute.
/// </summary>
[ApiController]
[Route("api/v1/assignment-actions")]
public class AssignmentActionsController : ControllerBase
{
    private readonly IAssignmentActionTokenService _assignmentActionTokenService;

    public AssignmentActionsController(IAssignmentActionTokenService assignmentActionTokenService)
    {
        _assignmentActionTokenService = assignmentActionTokenService;
    }

    /// <summary>
    /// Consumes an email Accept/Decline action token and applies it through the normal
    /// Accept/DeclineAsync path (including the existing agency-decline retry cascade).
    /// </summary>
    [HttpPost("respond")]
    public async Task<ActionResult<AssignmentResponseDto>> Respond([FromBody] AssignmentActionRespondDto request, CancellationToken cancellationToken)
    {
        var result = await _assignmentActionTokenService.ConsumeActionTokenAsync(request.Token, cancellationToken);
        return Ok(result);
    }
}
