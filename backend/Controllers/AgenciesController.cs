using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    /// Retrieves every Pending agency together with the compliance documents it has uploaded so far,
    /// for the admin verification queue.
    /// </summary>
    [HttpGet("verification-queue")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<IEnumerable<AgencyVerificationQueueItemDto>>> GetVerificationQueue(CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetVerificationQueueAsync(cancellationToken);
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
    /// Activates a verified agency.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult> ActivateAgency(Guid id, CancellationToken cancellationToken)
    {
        await _agencyService.ActivateAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
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

    [HttpPost("{id:guid}/compliance-docs")]
    [Authorize(Roles = nameof(UserRole.AgencyStaff))]
    public async Task<ActionResult<ComplianceDocResponseDto>> AddComplianceDoc(Guid id, [FromBody] ComplianceDocCreateDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.AddComplianceDocAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{id:guid}/compliance-docs")]
    [Authorize(Roles = $"{nameof(UserRole.AgencyStaff)},{nameof(UserRole.Admin)}")]
    public async Task<ActionResult<IEnumerable<ComplianceDocResponseDto>>> GetComplianceDocs(Guid id, CancellationToken cancellationToken)
    {
        var result = await _agencyService.GetComplianceDocsAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Replaces an existing compliance document's file/number/dates in place (e.g. re-uploading after
    /// a rejection, or renewing an expiring document). Resets the document back to Pending for
    /// re-verification. Distinct from <see cref="AddComplianceDoc"/>, which always inserts a new row
    /// and would collide with the one-live-document-per-type constraint if reused for replacement.
    /// </summary>
    [HttpPut("{id:guid}/compliance-docs/{docId:guid}")]
    [Authorize(Roles = nameof(UserRole.AgencyStaff))]
    public async Task<ActionResult<ComplianceDocResponseDto>> UpdateComplianceDoc(Guid id, Guid docId, [FromBody] ComplianceDocUpdateDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.UpdateComplianceDocAsync(id, docId, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Admin-only: marks a Pending compliance document as Verified.</summary>
    [HttpPost("{id:guid}/compliance-docs/{docId:guid}/verify")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ComplianceDocResponseDto>> VerifyComplianceDoc(Guid id, Guid docId, CancellationToken cancellationToken)
    {
        var result = await _agencyService.VerifyComplianceDocAsync(id, docId, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Admin-only: marks a Pending compliance document as Rejected.</summary>
    [HttpPost("{id:guid}/compliance-docs/{docId:guid}/reject")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ComplianceDocResponseDto>> RejectComplianceDoc(Guid id, Guid docId, CancellationToken cancellationToken)
    {
        var result = await _agencyService.RejectComplianceDocAsync(id, docId, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/vehicles")]
    [Authorize(Roles = nameof(UserRole.AgencyStaff))]
    public async Task<ActionResult<VehicleResponseDto>> AddVehicle(Guid id, [FromBody] VehicleCreateDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.AddVehicleAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Edits a vehicle's registration, type, and capacity without changing its status.</summary>
    [HttpPut("{id:guid}/vehicles/{vehicleId:guid}")]
    [Authorize(Roles = nameof(UserRole.AgencyStaff))]
    public async Task<ActionResult<VehicleResponseDto>> UpdateVehicle(
        Guid id,
        Guid vehicleId,
        [FromBody] VehicleUpdateDto request,
        CancellationToken cancellationToken)
    {
        var result = await _agencyService.UpdateVehicleAsync(
            id, vehicleId, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates a vehicle's availability for matching. OnTrip is system-managed by trip execution.</summary>
    [HttpPatch("{id:guid}/vehicles/{vehicleId:guid}/status")]
    [Authorize(Roles = nameof(UserRole.AgencyStaff))]
    public async Task<ActionResult<VehicleResponseDto>> UpdateVehicleStatus(
        Guid id,
        Guid vehicleId,
        [FromBody] UpdateVehicleStatusDto request,
        CancellationToken cancellationToken)
    {
        var result = await _agencyService.UpdateVehicleStatusAsync(
            id, vehicleId, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
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
    /// Updates an existing driver's editable details.
    /// </summary>
    [HttpPut("{id:guid}/drivers/{driverId:guid}")]
    [Authorize(Roles = nameof(UserRole.AgencyStaff))]
    public async Task<ActionResult<DriverResponseDto>> UpdateDriver(Guid id, Guid driverId, [FromBody] DriverUpdateDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.UpdateDriverAsync(id, driverId, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Removes or reinstates a driver on the agency's active roster (Active/Inactive only — OnTrip
    /// is managed exclusively by trip execution).
    /// </summary>
    [HttpPatch("{id:guid}/drivers/{driverId:guid}/status")]
    [Authorize(Roles = nameof(UserRole.AgencyStaff))]
    public async Task<ActionResult<DriverResponseDto>> UpdateDriverStatus(Guid id, Guid driverId, [FromBody] UpdateDriverStatusDto request, CancellationToken cancellationToken)
    {
        var result = await _agencyService.UpdateDriverStatusAsync(id, driverId, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
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
