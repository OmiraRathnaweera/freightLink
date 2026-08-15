using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Files;

/// <summary>
/// Payload for <c>POST /api/v1/loads/{loadId}/files</c> — links an already-uploaded file (from
/// <c>POST /api/v1/files/single</c>) to a load. Only references the upload by <see cref="PublicId"/>
/// and classifies it with <see cref="FileType"/>; url/size/content-type are never re-accepted here
/// since they're already durably stored on <c>UploadedFile</c> from the prior upload call.
/// </summary>
public class AttachLoadFileDto
{
    /// <summary>Cloudinary public id of an already-uploaded file, as returned by <c>POST /api/v1/files/single</c>.</summary>
    [Required]
    public string PublicId { get; set; } = string.Empty;

    /// <summary>
    /// Classification of why this file is attached to the load. Nullable so
    /// <see cref="RequiredAttribute"/> actually rejects an omitted JSON field instead of silently
    /// binding it to the enum's default member (mirrors why coordinate fields on
    /// <c>CreateLoadDto</c>/<c>UpdateLoadDto</c> are nullable).
    /// </summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FileType? FileType { get; set; }
}
