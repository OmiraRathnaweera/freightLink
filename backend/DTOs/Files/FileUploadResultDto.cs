namespace FreightLink.Api.DTOs.Files;

/// <summary>A single successfully-uploaded file's storage details, as returned by Cloudinary.</summary>
public class FileUploadResultDto
{
    /// <summary>Cloudinary's unique identifier for this asset — pass this back to delete it.</summary>
    public string PublicId { get; set; } = string.Empty;

    /// <summary>HTTPS URL the file can be fetched from.</summary>
    public string SecureUrl { get; set; } = string.Empty;

    /// <summary>
    /// File format/extension as Cloudinary detected it (e.g. "jpg", "pdf"), or <see langword="null"/>
    /// if Cloudinary didn't detect one — format detection is not guaranteed for <c>raw</c>-resource-type
    /// uploads (which every file uses here — see <see cref="Services.CloudinaryFileStorageService"/>),
    /// unlike images.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>Size of the stored file in bytes.</summary>
    public long Bytes { get; set; }

    /// <summary>Cloudinary's resource classification: "image", "video", or "raw".</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>The MIME type reported by the uploading client (e.g. "image/jpeg", "application/pdf").</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>The original filename supplied by the client, if available.</summary>
    public string? OriginalFileName { get; set; }
}
