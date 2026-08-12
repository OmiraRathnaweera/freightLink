using System.Net;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IFileStorageService" />
/// <remarks>
/// Every file is uploaded via <see cref="RawUploadParams"/>, i.e. Cloudinary's <c>raw</c> resource
/// type, for every file — including images. The strongly-typed <c>UploadAsync</c> overloads only
/// expose a single-shot "auto resource type" option through <c>AutoUploadParams</c>, which the SDK
/// only accepts on the chunked <c>UploadLargeAsync</c> path, not the normal one this feature needs
/// (verified directly against the installed CloudinaryDotNet 1.29.2 API, not assumed); each concrete
/// params type otherwise hardcodes its own resource type read-only. <c>raw</c> still stores and
/// serves any file correctly (including images, fetchable at <c>SecureUrl</c>) — the only feature
/// given up is Cloudinary's on-the-fly image transformations, which this generic file-upload
/// endpoint was never asked to provide. This also keeps delete simple: every publicId this service
/// issued is unambiguously a <c>raw</c> resource, so no resource-type guessing is needed there.
/// </remarks>
public class CloudinaryFileStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryFileStorageService> _logger;

    /// <summary>Creates the service with its DI-provided Cloudinary client (built from <see cref="Common.Options.CloudinaryOptions"/> in Program.cs).</summary>
    public CloudinaryFileStorageService(Cloudinary cloudinary, ILogger<CloudinaryFileStorageService> logger)
    {
        _cloudinary = cloudinary;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FileUploadResultDto> UploadFileAsync(IFormFile file, string? folder, CancellationToken cancellationToken = default)
    {
        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(file.FileName, file.OpenReadStream()),
            Folder = folder
        };

        RawUploadResult result;
        try
        {
            result = await _cloudinary.UploadAsync(uploadParams, null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Cloudinary upload threw for file {FileName}.", file.FileName);
            throw new ApiException(HttpStatusCode.InternalServerError, ErrorCode.FILE_UPLOAD_FAILED, "The file could not be uploaded.");
        }

        if (result.Error is not null)
        {
            _logger.LogError("Cloudinary upload failed for file {FileName}: {ErrorMessage}", file.FileName, result.Error.Message);
            throw new ApiException(HttpStatusCode.InternalServerError, ErrorCode.FILE_UPLOAD_FAILED, "The file could not be uploaded.");
        }

        _logger.LogInformation("Uploaded {PublicId} ({ResourceType}, {Bytes} bytes).", result.PublicId, result.ResourceType, result.Bytes);

        return new FileUploadResultDto
        {
            PublicId = result.PublicId,
            SecureUrl = result.SecureUrl?.ToString() ?? string.Empty,
            Format = result.Format,
            Bytes = result.Bytes,
            ResourceType = result.ResourceType,
            ContentType = file.ContentType,
            OriginalFileName = file.FileName
        };
    }

    /// <inheritdoc />
    public async Task<FileDeleteResultDto> DeleteFileAsync(string publicId, CancellationToken cancellationToken = default)
    {
        var deletionParams = new DeletionParams(publicId) { ResourceType = ResourceType.Raw };

        DeletionResult result;
        try
        {
            result = await _cloudinary.DestroyAsync(deletionParams);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Cloudinary delete threw for publicId {PublicId}.", publicId);
            throw new ApiException(HttpStatusCode.InternalServerError, ErrorCode.FILE_DELETE_FAILED, "The file could not be deleted.");
        }

        if (result.Result == "ok")
        {
            _logger.LogInformation("Deleted {PublicId}.", publicId);
            return new FileDeleteResultDto { PublicId = publicId, Deleted = true, Detail = result.Result };
        }

        if (result.Result != "not found")
        {
            _logger.LogError("Cloudinary delete returned unexpected result {Result} for publicId {PublicId}.", result.Result, publicId);
            throw new ApiException(HttpStatusCode.InternalServerError, ErrorCode.FILE_DELETE_FAILED, "The file could not be deleted.");
        }

        return new FileDeleteResultDto { PublicId = publicId, Deleted = false, Detail = "not found" };
    }
}
