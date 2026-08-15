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
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IFileUploadService" />
public class FileUploadService : IFileUploadService
{
    private readonly IFileStorageService _fileStorageService;
    private readonly AppDbContext _dbContext;
    private readonly CloudinaryOptions _cloudinaryOptions;
    private readonly ILogger<FileUploadService> _logger;

    /// <summary>Creates the service with its storage abstraction, DB context, Cloudinary settings (for the base folder), and logger.</summary>
    public FileUploadService(IFileStorageService fileStorageService, AppDbContext dbContext, IOptions<CloudinaryOptions> cloudinaryOptions, ILogger<FileUploadService> logger)
    {
        _fileStorageService = fileStorageService;
        _dbContext = dbContext;
        _cloudinaryOptions = cloudinaryOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FileUploadResultDto> UploadSingleAsync(IFormFile? file, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        FileUploadValidator.ValidateOrThrow(file);

        var result = await _fileStorageService.UploadFileAsync(file!, _cloudinaryOptions.Folder, cancellationToken);

        try
        {
            await PersistUploadMetadataAsync(result, currentUserId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist metadata for {PublicId} after a successful Cloudinary upload — attempting a compensating delete so the asset isn't orphaned.", result.PublicId);

            try
            {
                // CancellationToken.None: this cleanup must still run even if the original request
                // (and its token) was cancelled — that's one of the scenarios it exists to cover.
                await _fileStorageService.DeleteFileAsync(result.PublicId, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Compensating delete also failed for {PublicId} — this Cloudinary asset is now orphaned and needs manual cleanup.", result.PublicId);
            }

            throw;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<FileDeleteResultDto> DeleteSingleAsync(string publicId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "publicId is required.");
        }

        // No local record exists for an already-gone or never-tracked publicId — nothing to check
        // ownership against, so fall through to storage, which is idempotent for this case (see
        // FileDeleteResultDto). Enforcing ownership only when a record IS found keeps that
        // idempotent-delete behavior intact while still blocking cross-user deletion of known files.
        var existing = await _dbContext.UploadedFiles.FirstOrDefaultAsync(f => f.PublicId == publicId, cancellationToken);

        if (existing is not null)
        {
            if (existing.UploadedByUserId != currentUserId)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FILE_NOT_OWNED, "You do not have permission to delete this file.");
            }

            // Fast-path check — avoids attempting (and failing) the removal below in the common
            // non-race case. Not itself the authoritative guard: a load can be attached to this file
            // concurrently right after this check passes, which is why the DB removal below still
            // runs and can still fail on the Restrict FK.
            var isAttachedToLoad = await _dbContext.Files.AnyAsync(lf => lf.UploadedFileId == existing.FileId, cancellationToken);
            if (isAttachedToLoad)
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.FILE_IN_USE, "This file is attached to a load and cannot be deleted directly.");
            }

            // Remove the DB record BEFORE the storage delete below, not after: the FK from
            // LoadFiles.UploadedFileId is Restrict, so if a load was attached to this file
            // concurrently (after the check above), this fails here with 409 FILE_IN_USE — before the
            // real Cloudinary asset is ever touched. Deleting storage first would make that same race
            // irreversible, since the asset would already be gone by the time the DB caught it.
            await RemoveUploadMetadataAsync(existing, cancellationToken);
        }

        return await _fileStorageService.DeleteFileAsync(publicId, cancellationToken);
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

    /// <summary>
    /// Removes an uploaded file's metadata row, run BEFORE the real Cloudinary asset is deleted (see
    /// <see cref="DeleteSingleAsync"/>) so the irreversible storage delete never runs ahead of a DB
    /// state transition that can still fail. The Restrict FK from LoadFiles.UploadedFileId means this
    /// fails atomically if a load was attached to the file concurrently, which is translated to the
    /// documented 409 instead of an unhandled 500.
    /// </summary>
    private async Task RemoveUploadMetadataAsync(UploadedFile existing, CancellationToken cancellationToken)
    {
        _dbContext.UploadedFiles.Remove(existing);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503", ConstraintName: "FK_LoadFiles_UploadedFiles_UploadedFileId" })
        {
            // A load was attached to this file (POST /loads/{id}/files) between the fast-path check
            // in DeleteSingleAsync and this removal. Caught here, before any storage call has been
            // made, so the real asset is never deleted out from under the new attachment.
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.FILE_IN_USE, "This file is attached to a load and cannot be deleted directly.");
        }
    }
}
