namespace FreightLink.Api.Common.Options;

/// <summary>
/// Strongly-typed binding for the "Jwt" configuration section (env vars <c>JWT__*</c>),
/// used to sign/validate access tokens and size refresh-token lifetimes.
/// </summary>
public class JwtOptions
{
    /// <summary>Expected "iss" claim value; also the issuer JwtBearer validates incoming tokens against.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Expected "aud" claim value; also the audience JwtBearer validates incoming tokens against.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Symmetric signing/validation secret for access tokens. Never hardcode — env var only.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>How long an issued access token remains valid.</summary>
    public int AccessTokenMinutes { get; set; }

    /// <summary>How long an issued refresh token remains valid before it must be rotated via <c>/auth/refresh</c>.</summary>
    public int RefreshTokenDays { get; set; }
}
