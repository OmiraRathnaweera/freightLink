namespace FreightLink.Api.Entities;

/// <summary>
/// Storage/media metadata for a file uploaded via the shared <c>/api/v1/files</c> infrastructure
/// (<c>Services/CloudinaryFileStorageService.cs</c>) — the single source of truth for a file's URL,
/// format, size, and content type. Not linked to <see cref="Load"/> or any other business entity by
/// itself; components attach business meaning by referencing this row's <see cref="FileId"/> (e.g.
/// <see cref="LoadFile"/> for Component A) rather than duplicating these details. Removed when the
/// underlying Cloudinary asset is deleted via <c>IFileUploadService.DeleteSingleAsync</c>/<c>DeleteBatchAsync</c>.
/// </summary>
public class UploadedFile
{
    public Guid FileId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public string SecureUrl { get; set; } = string.Empty;
    public string? Format { get; set; }
    public long Bytes { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? OriginalFileName { get; set; }
    public DateTimeOffset UploadedAt { get; set; }

    public User UploadedByUser { get; set; } = null!;
}
