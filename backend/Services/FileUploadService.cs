using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Common.Validation;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Common;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Entities;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IFileUploadService" />
public class FileUploadService : IFileUploadService
{
    private readonly IFileStorageService _fileStorageService;
    private readonly AppDbContext _dbContext;
    private readonly CloudinaryOptions _cloudinaryOptions;

    /// <summary>Creates the service with its storage abstraction, DB context, and Cloudinary settings (for the base folder).</summary>
    public FileUploadService(IFileStorageService fileStorageService, AppDbContext dbContext, IOptions<CloudinaryOptions> cloudinaryOptions)
    {
        _fileStorageService = fileStorageService;
        _dbContext = dbContext;
        _cloudinaryOptions = cloudinaryOptions.Value;
    }

    /// <inheritdoc />
    public async Task<FileUploadResultDto> UploadSingleAsync(IFormFile? file, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        FileUploadValidator.ValidateOrThrow(file);

        var result = await _fileStorageService.UploadFileAsync(file!, _cloudinaryOptions.Folder, cancellationToken);
        await PersistUploadMetadataAsync(result, currentUserId, cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<FileDeleteResultDto> DeleteSingleAsync(string publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "publicId is required.");
        }

        var result = await _fileStorageService.DeleteFileAsync(publicId, cancellationToken);
        await RemoveUploadMetadataAsync(publicId, cancellationToken);
        return result;
    }

    /// <summary>Records a successfully-uploaded file's metadata so it's queryable outside of Cloudinary.</summary>
    private async Task PersistUploadMetadataAsync(FileUploadResultDto result, Guid uploadedByUserId, CancellationToken cancellationToken)
    {
        _dbContext.UploadedFiles.Add(new UploadedFile
        {
            PublicId = result.PublicId,
            SecureUrl = result.SecureUrl,
            Format = result.Format,
            Bytes = result.Bytes,
            ResourceType = result.ResourceType,
            ContentType = result.ContentType,
            OriginalFileName = result.OriginalFileName,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Removes a deleted file's metadata row, if one exists — a no-op for an already-untracked publicId.</summary>
    private async Task RemoveUploadMetadataAsync(string publicId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.UploadedFiles.FirstOrDefaultAsync(f => f.PublicId == publicId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _dbContext.UploadedFiles.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
