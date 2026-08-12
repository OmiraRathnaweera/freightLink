namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Shared limits for the file-upload feature, kept as a single source of truth so
/// <see cref="FileUploadValidator"/> has one place to check/update the accepted size and file types.
/// </summary>
public static class FileUploadPolicy
{
    /// <summary>Maximum accepted file size in bytes (10 MB).</summary>
    public const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>
    /// The only file extensions accepted for upload — image formats and PDF, matching the project's
    /// scope (cargo photos, PODs/PoPs, compliance documents). This is an allowlist, not a blocklist:
    /// any extension not listed here is rejected, including executables/scripts as well as other
    /// document types (e.g. <c>.docx</c>, <c>.zip</c>) that are out of scope for this project.
    /// </summary>
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".heic", ".heif", ".tif", ".tiff",
        ".pdf"
    };
}
