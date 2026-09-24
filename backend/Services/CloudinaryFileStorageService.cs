using System.Net;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.Configuration;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IFileStorageService" />
/// <remarks>
/// Every file is uploaded via <see cref="RawUploadParams"/>, i.e. Cloudinary's <c>raw</c> resource
/// type, for every file — including images. When Cloudinary is unreachable or credentials have restricted
/// permissions, this service gracefully falls back to local disk storage (<c>FILE_STORAGE_PATH</c>) so uploads never fail.
/// </remarks>
public class CloudinaryFileStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudinaryFileStorageService> _logger;

    /// <summary>Creates the service with its DI-provided Cloudinary client, configuration, and logger.</summary>
    public CloudinaryFileStorageService(Cloudinary cloudinary, IConfiguration configuration, ILogger<CloudinaryFileStorageService> logger)
    {
        _cloudinary = cloudinary;
        _configuration = configuration;
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

        RawUploadResult? result = null;
        try
        {
            result = await _cloudinary.UploadAsync(uploadParams, null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cloudinary upload threw for file {FileName}. Falling back to local storage.", file.FileName);
        }

        if (result is null || result.Error is not null)
        {
            if (result?.Error is not null)
            {
                _logger.LogWarning("Cloudinary upload failed for file {FileName}: {ErrorMessage}. Falling back to local storage.", file.FileName, result.Error.Message);
            }

            return await SaveLocallyAsync(file, folder, cancellationToken);
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

    private async Task<FileUploadResultDto> SaveLocallyAsync(IFormFile file, string? folder, CancellationToken cancellationToken)
    {
        var storagePath = _configuration["FILE_STORAGE_PATH"] ?? Path.Combine(Directory.GetCurrentDirectory(), "storage", "uploads");
        Directory.CreateDirectory(storagePath);

        var extension = Path.GetExtension(file.FileName);
        var uniqueId = Guid.NewGuid().ToString("N");
        var localFileName = $"{uniqueId}{extension}";
        var fullPath = Path.Combine(storagePath, localFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await using var readStream = file.OpenReadStream();
            if (readStream.CanSeek)
            {
                readStream.Position = 0;
            }
            await readStream.CopyToAsync(stream, cancellationToken);
        }

        var publicId = $"{folder ?? "freightlink"}/{uniqueId}";
        var secureUrl = $"/api/v1/files/content/{publicId}";

        _logger.LogInformation("Stored file locally at {FullPath} with publicId {PublicId}.", fullPath, publicId);

        return new FileUploadResultDto
        {
            PublicId = publicId,
            SecureUrl = secureUrl,
            Format = extension.TrimStart('.'),
            Bytes = file.Length,
            ResourceType = "raw",
            ContentType = file.ContentType,
            OriginalFileName = file.FileName
        };
    }

    /// <inheritdoc />
    public async Task<FileDeleteResultDto> DeleteFileAsync(string publicId, CancellationToken cancellationToken = default)
    {
        // Try local storage deletion first
        var storagePath = _configuration["FILE_STORAGE_PATH"] ?? Path.Combine(Directory.GetCurrentDirectory(), "storage", "uploads");
        var fileId = Path.GetFileName(publicId);
        if (Directory.Exists(storagePath))
        {
            var matching = Directory.GetFiles(storagePath, $"{fileId}.*");
            if (matching.Length > 0)
            {
                foreach (var f in matching)
                {
                    try { File.Delete(f); } catch { }
                }
                return new FileDeleteResultDto { PublicId = publicId, Deleted = true, Detail = "ok" };
            }
        }

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
