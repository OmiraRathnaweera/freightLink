using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// In-memory stand-in for <see cref="IFileStorageService"/>, registered by
/// <see cref="CustomWebApplicationFactory"/> so <c>FilesController</c> integration tests exercise
/// the real HTTP pipeline (routing, auth, model binding, <c>FileUploadService</c>'s validation)
/// without making a real Cloudinary call.
/// </summary>
public class FakeFileStorageService : IFileStorageService
{
    /// <inheritdoc />
    public Task<FileUploadResultDto> UploadFileAsync(IFormFile file, string? folder, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FileUploadResultDto
        {
            PublicId = $"fake/{Guid.NewGuid():N}",
            SecureUrl = "https://res.cloudinary.com/fake/raw/upload/fake.bin",
            // Real Cloudinary raw-resource-type uploads don't always detect a format — null here
            // (rather than a hardcoded value) is what caught the original bug: FileUploadResultDto/
            // UploadedFile.Format not being nullable, which broke persistence against real Postgres.
            Format = null,
            Bytes = file.Length,
            ResourceType = "raw",
            ContentType = file.ContentType,
            OriginalFileName = file.FileName
        });

    /// <inheritdoc />
    public Task<FileDeleteResultDto> DeleteFileAsync(string publicId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FileDeleteResultDto { PublicId = publicId, Deleted = true, Detail = "ok" });
}
