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
    /// Lists all vehicles in an agency's fleet.
    /// </summary>
    [HttpGet("{id:guid}/vehicles")]
    [Authorize(Roles = $"{nameof(UserRole.AgencyStaff)},{nameof(UserRole.Admin)}")]
    public async Task<ActionResult<List<VehicleResponseDto>>> GetVehicles(Guid id, CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetVehiclesAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lists all drivers employed by an agency.
    /// </summary>
    [HttpGet("{id:guid}/drivers")]
    [Authorize(Roles = $"{nameof(UserRole.AgencyStaff)},{nameof(UserRole.Admin)}")]
    public async Task<ActionResult<List<DriverResponseDto>>> GetDrivers(Guid id, CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetDriversAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Onboards/adds a driver to an agency.
    /// </summary>
    [HttpPost("{id:guid}/drivers")]
    [Authorize(Roles = $"{nameof(UserRole.AgencyStaff)},{nameof(UserRole.Admin)}")]
    public async Task<ActionResult<DriverResponseDto>> AddDriver(Guid id, [FromBody] CreateDriverRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.AddDriverAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Convenience endpoint for Agency Staff to fetch their own agency's fleet (vehicles + drivers),
    /// or for Admin to fetch an agency's fleet by specifying agencyId.
    /// </summary>
    [HttpGet("my/fleet")]
    [Authorize(Roles = $"{nameof(UserRole.AgencyStaff)},{nameof(UserRole.Admin)}")]
    public async Task<ActionResult<AgencyFleetResponseDto>> GetMyFleet([FromQuery] Guid? agencyId, CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetFleetAsync(agencyId, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full fleet resources (vehicles + drivers) for a specific agency.
    /// </summary>
    [HttpGet("{id:guid}/fleet")]
    [Authorize(Roles = $"{nameof(UserRole.AgencyStaff)},{nameof(UserRole.Admin)}")]
    public async Task<ActionResult<AgencyFleetResponseDto>> GetFleet(Guid id, CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetFleetAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Seeds default active carrier agencies across Sri Lanka for testing and demonstration.
    /// </summary>
    [HttpPost("seed-defaults")]
    [AllowAnonymous]
    public async Task<ActionResult> SeedDefaultAgencies(CancellationToken cancellationToken)
    {
        await _agencyService.SeedDefaultAgenciesIfNotExistsAsync(cancellationToken);
        return Ok(new { message = "Default active carrier agencies seeded successfully." });
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
