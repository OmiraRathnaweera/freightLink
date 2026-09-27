using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Trips;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Trip execution endpoints (Component C): list/detail, status advancement, and proof-of-pickup /
/// proof-of-delivery evidence capture. Deliberately thin — every action just extracts the caller's
/// identity/role from the access token and delegates to <see cref="ITripService"/>, which owns all
/// business rules including the four-way ownership resolution described on that interface. Mirrors
/// <c>LoadsController</c>'s structure for consistency across controllers.
///
/// <para>
/// <b>Skeleton status:</b> routes, auth attributes, and DTO wiring are final per the API contract
/// (Section 4.4); <see cref="Services.TripService"/> (the injected <see cref="ITripService"/>
/// implementation) is a stub that throws <see cref="NotImplementedException"/> on every call until
/// Component C's business logic is written.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/trips")]
[Authorize]
public class TripsController : ControllerBase
{
    private const string AgencyStaffOrDriverRoles = nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Driver);

    /// <summary>Role authorized to dispatch trips (Requirement 3: only agencies dispatch trips).</summary>
    private const string AgencyStaffRole = nameof(UserRole.AgencyStaff);

    /// <summary>Roles authorized to update, cancel, or delete trips (Requirement 4: drivers strictly excluded).</summary>
    private const string AgencyStaffOrAdminRoles = nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Admin);

    /// <summary>See remarks on <see cref="ShipperOrAdminRoles"/> for why these are <see langword="nameof"/>-built, not string literals.</summary>
    private const string AgencyStaffOrDriverOrAdminRoles = nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Driver) + "," + nameof(UserRole.Admin);

    /// <summary>
    /// <see cref="Authorize"/>'s <c>Roles</c> property must be a compile-time constant, so it can't
    /// take a <see cref="UserRole"/> value directly — these use <see langword="nameof"/> instead of
    /// string literals so a renamed enum member fails to compile here rather than silently desyncing
    /// from the actual role name. Mirrors <c>LoadsController</c>'s identical convention.
    /// </summary>
    private const string ShipperOrAdminRoles = nameof(UserRole.Shipper) + "," + nameof(UserRole.Admin);

    /// <summary>Every role that may reach a trip in some "own" capacity, for the detail/evidence-list endpoints.</summary>
    private const string AnyTripRole = nameof(UserRole.Shipper) + "," + nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Driver) + "," + nameof(UserRole.Admin);

    private readonly ITripService _tripService;

    /// <summary>Creates the controller with its injected trip service.</summary>
    public TripsController(ITripService tripService)
    {
        _tripService = tripService;
    }

    /// <summary>
    /// Dispatches and creates a new trip, assigning an agency vehicle and driver to an accepted assignment.
    /// Only the respective agency staff may dispatch trips (Requirement 3).
    /// </summary>
    /// <param name="request">The creation payload including assignment, vehicle, and driver ids.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 Created with the created <see cref="TripResponseDto"/>.</returns>
    [HttpPost]
    [Authorize(Roles = AgencyStaffRole)]
    public async Task<ActionResult<TripResponseDto>> Create([FromBody] CreateTripDto request, CancellationToken cancellationToken)
    {
        var result = await _tripService.CreateAsync(request, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.TripId }, result);
    }

    /// <summary>
    /// Lists/filters/paginates trips (the AgencyStaff/Driver/Admin dashboard view). Per the API
    /// contract, Shipper is not a valid caller here — see <see cref="GetById"/> for the Shipper-facing
    /// single-trip view instead.
    /// </summary>
    /// <param name="query">Search/filter/sort/paging parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with a <see cref="PagedTripResponseDto"/>.</returns>
    [HttpGet]
    [Authorize(Roles = AgencyStaffOrDriverOrAdminRoles)]
    public async Task<ActionResult<PagedTripResponseDto>> GetList([FromQuery] TripListQueryDto query, CancellationToken cancellationToken)
    {
        var result = await _tripService.GetListAsync(query, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Fetches a single trip's detail, including its status timeline and captured evidence. Unlike
    /// <see cref="GetList"/>, a Shipper may call this for a trip on their own load.
    /// </summary>
    /// <param name="id">The trip's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the matching <see cref="TripResponseDto"/>.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = AnyTripRole)]
    public async Task<ActionResult<TripResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _tripService.GetByIdAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Modifies an existing trip's vehicle or driver assignment prior to departure (while still in Assigned status).
    /// </summary>
    /// <param name="id">The trip's id.</param>
    /// <param name="request">The updated vehicle and/or driver ids with optional notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the updated <see cref="TripResponseDto"/>.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AgencyStaffOrAdminRoles)]
    public async Task<ActionResult<TripResponseDto>> Update(Guid id, [FromBody] UpdateTripDto request, CancellationToken cancellationToken)
    {
        var result = await _tripService.UpdateAsync(id, request, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Advances a trip's status. Hard-blocked at the database level (per ADR-004) from reaching
    /// <c>PickedUp</c>/<c>Delivered</c> without the matching evidence already captured via
    /// <see cref="UploadEvidence"/>.
    /// </summary>
    /// <param name="id">The trip's id.</param>
    /// <param name="request">The target status and optional notes/GPS snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the trip in its new status.</returns>
    [HttpPost("{id:guid}/status")]
    [Authorize(Roles = AgencyStaffOrDriverRoles)]
    public async Task<ActionResult<TripResponseDto>> ChangeStatus(Guid id, [FromBody] ChangeTripStatusDto request, CancellationToken cancellationToken)
    {
        var result = await _tripService.ChangeStatusAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Permanently deletes a trip record and unclaims the associated shipment load (reverting its status to Posted).
    /// Only Agency Staff may delete trips (Requirement 4).
    /// </summary>
    /// <param name="id">The trip's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 NoContent upon successful deletion.</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AgencyStaffOrAdminRoles)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _tripService.DeleteAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Cancels an active trip (Assigned, PickedUp, or InTransit) with an optional request body.
    /// Only Agency Staff may cancel trips (Requirement 4).
    /// </summary>
    /// <param name="id">The trip's id.</param>
    /// <param name="request">Optional cancellation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the cancelled <see cref="TripResponseDto"/>.</returns>
    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Roles = AgencyStaffOrAdminRoles)]
    public async Task<ActionResult<TripResponseDto>> Cancel(Guid id, [FromBody] CancelTripDto? request, CancellationToken cancellationToken)
    {
        var result = await _tripService.CancelAsync(id, request, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Captures proof-of-pickup or proof-of-delivery by linking an already-uploaded file (from
    /// <c>POST /api/v1/files/single</c>). AgencyStaff may only submit <c>PickupProof</c>; Driver may
    /// only submit <c>DeliveryProof</c> — enforced by the service layer, not this controller.
    /// </summary>
    /// <param name="id">The trip's id.</param>
    /// <param name="request">The uploaded file's public id, evidence type, and optional GPS coordinates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the created <see cref="TripEvidenceResponseDto"/>.</returns>
    [HttpPost("{id:guid}/evidence")]
    [Authorize(Roles = AgencyStaffOrDriverRoles)]
    public async Task<ActionResult<TripEvidenceResponseDto>> UploadEvidence(Guid id, [FromBody] UploadTripEvidenceDto request, CancellationToken cancellationToken)
    {
        var result = await _tripService.UploadEvidenceAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Lists captured evidence for a trip.</summary>
    /// <param name="id">The trip's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the trip's <see cref="TripEvidenceResponseDto"/> rows.</returns>
    [HttpGet("{id:guid}/evidence")]
    [Authorize(Roles = AnyTripRole)]
    public async Task<ActionResult<List<TripEvidenceResponseDto>>> GetEvidence(Guid id, CancellationToken cancellationToken)
    {
        var result = await _tripService.GetEvidenceAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Seeds complete example testing trips (Assigned, InTransit, Delivered) with full hierarchy
    /// for Swagger testing and UI verification. AllowAnonymous for convenient local dev use.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of seeded trips.</returns>
    [HttpPost("seed-example")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TripResponseDto>>> SeedExample(CancellationToken cancellationToken)
    {
        var result = await _tripService.SeedExampleTripsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Extracts the authenticated user's id from the <c>NameIdentifier</c> claim on the access token.</summary>
    /// <returns>The caller's user id.</returns>
    /// <exception cref="ApiException">401 if the claim is absent or not a well-formed GUID.</exception>
    /// <remarks>
    /// Identical to <c>LoadsController</c>'s private helper of the same name — a candidate for
    /// extraction into a shared base controller if a third controller ends up needing it too, but not
    /// done here to keep this skeleton a pure addition with no changes to existing files.
    /// </remarks>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid user id.");
        }

        return userId;
    }

    /// <summary>Extracts the authenticated user's role from the <c>Role</c> claim on the access token.</summary>
    /// <returns>The caller's role.</returns>
    /// <exception cref="ApiException">401 if the claim is absent or not a recognized role.</exception>
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