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
    /// one exists. Idempotent for an unknown/already-gone id — no ownership check applies since
    /// there is no local record to check against (see <see cref="FileDeleteResultDto"/>).
    /// </summary>
    /// <param name="publicId">The storage provider's unique id for the file.</param>
    /// <param name="currentUserId">The authenticated caller's id, checked against the file's recorded uploader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the file existed and was removed.</returns>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 400 if <paramref name="publicId"/> is blank; 403 if a record exists and
    /// <paramref name="currentUserId"/> is not its uploader; 409 if it's still attached to a Load;
    /// 500 if the storage provider fails the delete for a reason other than "not found", or if the
    /// metadata row can't be removed after a successful storage delete.
    /// </exception>
    Task<FileDeleteResultDto> DeleteSingleAsync(string publicId, Guid currentUserId, CancellationToken cancellationToken = default);
}
