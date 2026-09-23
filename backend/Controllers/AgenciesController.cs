using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Handles Basic CRUD operations for Agencies.
/// </summary>
[ApiController]
[Route("api/v1/agencies")]
[Authorize]
public class AgenciesController : ControllerBase
{
    private readonly IAgencyService _agencyService;

    public AgenciesController(IAgencyService agencyService)
    {
        _agencyService = agencyService;
    }

    /// <summary>
    /// Creates a new agency in a pending status.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<AgencyResponseDto>> CreateAgency([FromBody] AgencyCreateDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.CreateAsync(GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        // Returns a 201 Created response pointing to the GetAgency endpoint (which we define below)
        return CreatedAtAction(nameof(GetAgency), new { id = result.AgencyId }, result);
    }

    /// <summary>
    /// Retrieves a specific agency by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{nameof(UserRole.AgencyStaff)},{nameof(UserRole.Admin)}")]
    public async Task<ActionResult<AgencyResponseDto>> GetAgency(Guid id, CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetByIdAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a list of all agencies.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<PagedAgencyResponseDto>> GetAllAgencies([FromQuery] AgencyListQueryDto query, CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetListAsync(GetCurrentUserId(), GetCurrentUserRole(), query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a list of agencies with compliance documents expiring soon.
    /// </summary>
    [HttpGet("expiring-compliance")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<IEnumerable<AgencyExpiringComplianceDto>>> GetExpiringCompliance([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var result = await _agencyService.GetAgenciesWithExpiringComplianceAsync(days, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates an existing agency's profile details.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{nameof(UserRole.AgencyStaff)},{nameof(UserRole.Admin)}")]
    public async Task<ActionResult<AgencyResponseDto>> UpdateAgency(Guid id, [FromBody] AgencyUpdateDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.UpdateAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Approves/verifies a pending agency.
    /// </summary>
    [HttpPost("{id:guid}/verify")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult> VerifyAgency(Guid id, CancellationToken cancellationToken)
    {
        await _agencyService.VerifyAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Suspends an agency.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult> SuspendAgency(Guid id, CancellationToken cancellationToken)
    {
        await _agencyService.SuspendAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok();
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
