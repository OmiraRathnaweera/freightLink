using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Shared file upload/delete endpoints, backed by Cloudinary. Component-agnostic infrastructure —
/// intended for reuse by Component A (Load files) and Component C (TripEvidence) rather than being
/// tied to either. Deliberately thin — every action just delegates to <see cref="IFileUploadService"/>.
/// Restricted to <see cref="UserRole.Shipper"/>, <see cref="UserRole.AgencyStaff"/>, and
/// <see cref="UserRole.Driver"/> — every role that actually attaches evidence/documents to a load
/// or trip. <see cref="UserRole.Admin"/> is deliberately excluded: file upload/delete is an
/// operational action taken by the party producing the file, not an oversight action. There is no
/// per-resource ownership concept here since this controller has no notion of which business entity
/// a file belongs to — the caller's id is only recorded as the uploader on
/// <see cref="Entities.UploadedFile"/>, not used as an access-control check.
/// </summary>
[ApiController]
[Route("api/v1/files")]
[Authorize(Roles = AllowedRoles)]
public class FilesController : ControllerBase
{
    private const string AllowedRoles =
        nameof(UserRole.Shipper) + "," + nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Driver);

    private readonly IFileUploadService _fileUploadService;

    /// <summary>Creates the controller with its injected file upload service.</summary>
    public FilesController(IFileUploadService fileUploadService)
    {
        _fileUploadService = fileUploadService;
    }

    /// <summary>Uploads a single file.</summary>
    /// <param name="file">The file to upload (multipart/form-data).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with the uploaded file's storage details.</returns>
    [HttpPost("single")]
    public async Task<ActionResult<FileUploadResultDto>> UploadSingle(IFormFile? file, CancellationToken cancellationToken)
    {
        var result = await _fileUploadService.UploadSingleAsync(file, GetCurrentUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Deletes a single file by its Cloudinary public id. Idempotent — deleting an already-gone or
    /// unknown id returns 200 with <c>deleted: false</c>, never a 404 (see <see cref="FileDeleteResultDto"/>).
    /// </summary>
    /// <param name="publicId">
    /// The Cloudinary public id, which may itself contain <c>/</c> characters (folder-namespaced) —
    /// bound via a catch-all route parameter so the full id is captured.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the delete outcome.</returns>
    [HttpDelete("{*publicId}")]
    public async Task<ActionResult<FileDeleteResultDto>> DeleteSingle(string publicId, CancellationToken cancellationToken)
    {
        var result = await _fileUploadService.DeleteSingleAsync(publicId, cancellationToken);
        return Ok(result);
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
}
