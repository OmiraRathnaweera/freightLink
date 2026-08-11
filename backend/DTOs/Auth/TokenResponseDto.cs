namespace FreightLink.Api.DTOs.Auth;

/// <summary>
/// Response for <c>POST /api/v1/auth/login</c> and <c>POST /api/v1/auth/refresh</c>.
/// Deliberately carries only these two fields — never the user profile.
/// </summary>
public class TokenResponseDto
{
    /// <summary>Short-lived JWT used as <c>Authorization: Bearer &lt;accessToken&gt;</c> on protected endpoints.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Long-lived, single-use token exchanged at <c>/auth/refresh</c> for a new token pair.</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
