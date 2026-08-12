using FreightLink.Api.DTOs.Files;
using Microsoft.AspNetCore.Http;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Validation + orchestration layer over <see cref="IFileStorageService"/> — the service
/// <c>FilesController</c> actually calls. Enforces <see cref="Common.Validation.FileUploadPolicy"/>
/// (size limit, blocked extensions) before any file reaches storage.
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Validates and uploads a single file, then persists its metadata to the <c>UploadedFiles</c>
    /// table so it's discoverable outside of Cloudinary (this row carries no link to any business
    /// entity yet — see <see cref="Entities.UploadedFile"/>).
    /// </summary>
    /// <param name="file">The file to upload, or <see langword="null"/> if none was attached.</param>
    /// <param name="currentUserId">The authenticated caller's id, recorded as the uploader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored file's public id, URL, and metadata.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 400 if the file is missing, has a blocked extension, or exceeds the size limit; 500 if the
    /// storage provider fails the upload.
    /// </exception>
    Task<FileUploadResultDto> UploadSingleAsync(IFormFile? file, Guid currentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a single file by its public id, and removes its <c>UploadedFiles</c> metadata row if
    /// one exists. Idempotent — a missing id is not an error (see <see cref="FileDeleteResultDto"/>).
    /// </summary>
    /// <param name="publicId">The storage provider's unique id for the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the file existed and was removed.</returns>
    /// <exception cref="Common.Exceptions.ApiException">400 if <paramref name="publicId"/> is blank; 500 if the storage provider fails the delete for a reason other than "not found".</exception>
    Task<FileDeleteResultDto> DeleteSingleAsync(string publicId, CancellationToken cancellationToken = default);
}
