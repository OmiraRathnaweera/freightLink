namespace FreightLink.Api.Common.Options;

/// <summary>
/// Shared secret guarding internal-only, non-JWT endpoints (e.g. <c>POST /internal/pricing/estimate</c>),
/// read from the flat <c>INTERNAL_API_KEY</c> env var. Checked by <c>InternalApiKeyAuthFilter</c>
/// against the caller's <c>X-Internal-Api-Key</c> header.
/// </summary>
public class InternalApiOptions
{
    /// <summary>The expected value of the <c>X-Internal-Api-Key</c> header. Every request is rejected if unset.</summary>
    public string? ApiKey { get; set; }
}
