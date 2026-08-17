using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Files;

/// <summary>
/// Response shape for a load's attached file — combines the <c>LoadFile</c> link (id, classification,
/// attach time) with the joined <c>UploadedFile</c> storage details a client needs to render it,
/// without the client needing a second call to look up the underlying upload.
/// </summary>
public class LoadFileResponseDto
{
    /// <summary>The <c>LoadFile</c> link's own id.</summary>
    public Guid FileId { get; set; }

    /// <summary>The load this file is attached to.</summary>
    public Guid LoadId { get; set; }

    /// <summary>Classification of why this file is attached to the load.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FileType FileType { get; set; }

    /// <summary>When this file was attached to the load.</summary>
    public DateTimeOffset AttachedAt { get; set; }

    /// <summary>Cloudinary public id of the underlying upload.</summary>
    public string PublicId { get; set; } = string.Empty;

    /// <summary>Publicly-accessible URL of the underlying upload.</summary>
    public string SecureUrl { get; set; } = string.Empty;

    /// <summary>Cloudinary-detected file format, if any.</summary>
    public string? Format { get; set; }

    /// <summary>Size of the underlying upload in bytes.</summary>
    public long Bytes { get; set; }

    /// <summary>Content type of the underlying upload, as supplied at upload time.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Original client-supplied file name of the underlying upload, if any.</summary>
    public string? OriginalFileName { get; set; }
}
