using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Agency;
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
    public async Task<ActionResult<AgencyResponseDto>> CreateAgency([FromBody] AgencyCreateDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.CreateAsync(GetCurrentUserId(), request, cancellationToken);
        // Returns a 201 Created response pointing to the GetAgency endpoint (which we define below)
        return CreatedAtAction(nameof(GetAgency), new { id = result.AgencyId }, result);
    }

    /// <summary>
    /// Retrieves a specific agency by its ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AgencyResponseDto>> GetAgency(Guid id, CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a list of all agencies.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AgencyResponseDto>>> GetAllAgencies(CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates an existing agency's profile details.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<AgencyResponseDto>> UpdateAgency(Guid id, [FromBody] AgencyUpdateDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.UpdateAsync(id, request, cancellationToken);
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
}
