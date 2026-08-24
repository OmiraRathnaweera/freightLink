using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Links already-uploaded files (see <see cref="FilesController"/>) to a load (Component A: Manifest,
/// Invoice, CargoPhoto, Other). Deliberately thin — every action just extracts the caller's
/// identity/role and delegates to <see cref="ILoadFileService"/>, which owns all business rules
/// including ownership enforcement. Never talks to Cloudinary itself.
/// </summary>
[ApiController]
[Route("api/v1/loads/{loadId:guid}/files")]
[Authorize]
public class LoadFilesController : ControllerBase
{
    /// <summary>
    /// <see cref="Authorize"/>'s <c>Roles</c> property must be a compile-time constant, so it can't
    /// take a <see cref="UserRole"/> value directly — <see langword="nameof"/> is used instead of a
    /// string literal so a renamed enum member fails to compile here rather than silently desyncing.
    /// </summary>
    private const string ShipperRole = nameof(UserRole.Shipper);

    /// <summary>See <see cref="ShipperRole"/>.</summary>
    private const string ShipperOrAdminRoles = nameof(UserRole.Shipper) + "," + nameof(UserRole.Admin);

    private readonly ILoadFileService _loadFileService;

    /// <summary>Creates the controller with its injected load file service.</summary>
    public LoadFilesController(ILoadFileService loadFileService)
    {
        _loadFileService = loadFileService;
    }

    /// <summary>Attaches an already-uploaded file to a load. Only the owning Shipper may attach.</summary>
    /// <param name="loadId">The load to attach the file to.</param>
    /// <param name="request">The upload reference and its classification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the created <see cref="LoadFileResponseDto"/>.</returns>
    [HttpPost]
    [Authorize(Roles = ShipperRole)]
    public async Task<ActionResult<LoadFileResponseDto>> Attach(Guid loadId, [FromBody] AttachLoadFileDto request, CancellationToken cancellationToken)
    {
        var result = await _loadFileService.AttachAsync(loadId, GetCurrentUserId(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Lists a load's attached files. A Shipper may only list a load they own; an Admin may list any load.
    /// </summary>
    /// <param name="loadId">The load whose files to list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of attached files.</returns>
    [HttpGet]
    [Authorize(Roles = ShipperOrAdminRoles)]
    public async Task<ActionResult<List<LoadFileResponseDto>>> List(Guid loadId, CancellationToken cancellationToken)
    {
        var result = await _loadFileService.ListAsync(loadId, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Detaches a file from a load. Only the owning Shipper may detach. Removes only the attachment
    /// link — the underlying uploaded file itself is untouched and remains separately deletable via
    /// <see cref="FilesController.DeleteSingle"/> once it's no longer attached to any load.
    /// </summary>
    /// <param name="loadId">The load the file is attached to.</param>
    /// <param name="fileId">The attachment's own id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("{fileId:guid}")]
    [Authorize(Roles = ShipperRole)]
    public async Task<IActionResult> Detach(Guid loadId, Guid fileId, CancellationToken cancellationToken)
    {
        await _loadFileService.DetachAsync(loadId, fileId, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    /// <summary>Extracts the authenticated user's id from the <c>NameIdentifier</c> claim on the access token.</summary>
    /// <returns>The caller's user id.</returns>
    /// <exception cref="ApiException">401 if the claim is absent or not a well-formed GUID.</exception>
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
