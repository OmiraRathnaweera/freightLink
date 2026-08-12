using FreightLink.Api.DTOs.Files;
using Microsoft.AspNetCore.Http;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Pure storage abstraction over Cloudinary — uploads/deletes a single already-validated file.
/// Carries no validation or business rules of its own (see <see cref="IFileUploadService"/> for
/// that layer); this exists so Component A (Load files) and Component C (TripEvidence) can depend
/// on file storage without depending on the Cloudinary SDK directly, and so the storage backend
/// could be swapped later without touching either component.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Uploads a single file to storage.</summary>
    /// <param name="file">The file to upload. Caller is responsible for validating it first.</param>
    /// <param name="folder">Optional folder path to namespace the upload under, appended to the configured base folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored file's public id, URL, and metadata.</returns>
    /// <exception cref="Common.Exceptions.ApiException">500 <c>FILE_UPLOAD_FAILED</c> if the storage provider rejects or fails the upload.</exception>
    Task<FileUploadResultDto> UploadFileAsync(IFormFile file, string? folder, CancellationToken cancellationToken = default);

    /// <summary>Deletes a single file from storage by its public id. Idempotent — deleting a missing id is not an error.</summary>
    /// <param name="publicId">The storage provider's unique id for the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the file existed and was removed, plus the provider's raw result detail.</returns>
    /// <exception cref="Common.Exceptions.ApiException">500 <c>FILE_DELETE_FAILED</c> if the storage provider fails the delete for a reason other than "not found".</exception>
    Task<FileDeleteResultDto> DeleteFileAsync(string publicId, CancellationToken cancellationToken = default);
}
