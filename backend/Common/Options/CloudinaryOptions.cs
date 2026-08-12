namespace FreightLink.Api.Common.Options;

/// <summary>
/// Strongly-typed binding for the "Cloudinary" configuration section (env vars
/// <c>CLOUDINARY__*</c>), used to construct the singleton Cloudinary SDK client in
/// <c>Program.cs</c> for <see cref="Services.CloudinaryFileStorageService"/>.
/// </summary>
public class CloudinaryOptions
{
    /// <summary>Cloudinary account cloud name.</summary>
    public string CloudName { get; set; } = string.Empty;

    /// <summary>Cloudinary API key. Never hardcode — env var only.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Cloudinary API secret. Never hardcode — env var only.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Optional base folder every upload is namespaced under (e.g. "freightlink").</summary>
    public string? Folder { get; set; }
}
