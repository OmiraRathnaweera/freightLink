using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="ILoadFileService" />
public class LoadFileService : ILoadFileService
{
    private readonly AppDbContext _dbContext;

    /// <summary>Creates the service with its DB context.</summary>
    public LoadFileService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<LoadFileResponseDto> AttachAsync(Guid loadId, Guid currentUserId, AttachLoadFileDto request, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (load.ShipperUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "This load does not belong to the authenticated caller.");
        }

        var uploadedFile = await _dbContext.UploadedFiles.FirstOrDefaultAsync(f => f.PublicId == request.PublicId, cancellationToken);

        if (uploadedFile is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_FILE_UPLOAD_NOT_FOUND, "The referenced uploaded file could not be found.");
        }

        if (uploadedFile.UploadedByUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FILE_NOT_OWNED, "You do not have permission to attach this file.");
        }

        // Fast-path check — avoids attempting (and failing) an insert in the common non-race case.
        // Not itself the authoritative guard: two concurrent attaches of the same UploadedFile can
        // both pass this check before either commits, so the actual race is closed by catching the
        // DB's uq_loadfile_uploadedfileid unique-index violation below.
        var alreadyAttached = await _dbContext.Files.AnyAsync(lf => lf.UploadedFileId == uploadedFile.FileId, cancellationToken);
        if (alreadyAttached)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.FILE_IN_USE, "This file is already attached to a load.");
        }

        var loadFile = new LoadFile
        {
            FileId = Guid.NewGuid(),
            LoadId = loadId,
            UploadedFileId = uploadedFile.FileId,
            FileType = request.FileType!.Value,
            AttachedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Files.Add(loadFile);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_loadfile_uploadedfileid" })
        {
            // Two concurrent attaches of the same UploadedFile both passed the fast-path check above;
            // the unique index is what actually stops the second one — translated to the same
            // documented 409 rather than surfacing as an unhandled 500.
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.FILE_IN_USE, "This file is already attached to a load.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503", ConstraintName: "FK_LoadFiles_UploadedFiles_UploadedFileId" })
        {
            // The referenced UploadedFile was deleted (DELETE /files/{publicId}) between the read
            // above and this insert. From the caller's perspective it no longer exists to attach to —
            // the same 404 as if it had never been found in the first place.
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_FILE_UPLOAD_NOT_FOUND, "The referenced uploaded file could not be found.");
        }

        return MapToResponse(loadFile, uploadedFile);
    }

    /// <inheritdoc />
    public async Task<List<LoadFileResponseDto>> ListAsync(Guid loadId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.AsNoTracking().FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (currentUserRole != UserRole.Admin && load.ShipperUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "This load does not belong to the authenticated caller.");
        }

        var loadFiles = await _dbContext.Files
            .AsNoTracking()
            .Include(lf => lf.UploadedFile)
            .Where(lf => lf.LoadId == loadId)
            .OrderByDescending(lf => lf.AttachedAt)
            .ToListAsync(cancellationToken);

        return loadFiles.Select(lf => MapToResponse(lf, lf.UploadedFile)).ToList();
    }

    /// <inheritdoc />
    public async Task DetachAsync(Guid loadId, Guid fileId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var load = await _dbContext.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId, cancellationToken);

        if (load is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_NOT_FOUND, "The requested load could not be found.");
        }

        if (load.ShipperUserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.LOAD_NOT_OWNED, "This load does not belong to the authenticated caller.");
        }

        var loadFile = await _dbContext.Files.FirstOrDefaultAsync(lf => lf.FileId == fileId && lf.LoadId == loadId, cancellationToken);

        if (loadFile is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.LOAD_FILE_NOT_FOUND, "The requested file attachment could not be found on this load.");
        }

        _dbContext.Files.Remove(loadFile);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Maps a <see cref="LoadFile"/> link joined with its <see cref="UploadedFile"/> to the wire response.</summary>
    private static LoadFileResponseDto MapToResponse(LoadFile loadFile, UploadedFile uploadedFile) => new()
    {
        FileId = loadFile.FileId,
        LoadId = loadFile.LoadId,
        FileType = loadFile.FileType,
        AttachedAt = loadFile.AttachedAt,
        PublicId = uploadedFile.PublicId,
        SecureUrl = uploadedFile.SecureUrl,
        Format = uploadedFile.Format,
        Bytes = uploadedFile.Bytes,
        ContentType = uploadedFile.ContentType,
        OriginalFileName = uploadedFile.OriginalFileName
    };
}
