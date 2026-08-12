namespace FreightLink.Api.DTOs.Files;

/// <summary>
/// Result of deleting a single file. Delete is idempotent: deleting an already-gone or
/// never-existing <see cref="PublicId"/> is not an error — it returns <see cref="Deleted"/> =
/// <see langword="false"/> with <see cref="Detail"/> = <c>"not found"</c>, mirroring Cloudinary's
/// own idempotent <c>destroy</c> semantics, rather than a thrown 404.
/// </summary>
public class FileDeleteResultDto
{
    /// <summary>The Cloudinary public id that was targeted.</summary>
    public string PublicId { get; set; } = string.Empty;

    /// <summary>Whether the asset actually existed and was removed.</summary>
    public bool Deleted { get; set; }

    /// <summary>Cloudinary's raw result string (e.g. "ok", "not found").</summary>
    public string Detail { get; set; } = string.Empty;
}
