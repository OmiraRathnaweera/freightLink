namespace FreightLink.Api.DTOs.Auth;

/// <summary>
/// Response for both <c>POST /api/v1/auth/register/shipper</c> and
/// <c>POST /api/v1/auth/register/agency</c> — a success confirmation only; the client must
/// call <c>/auth/login</c> separately to obtain tokens.
/// </summary>
public class RegisterResponseDto
{
    /// <summary>Human-readable confirmation message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The id of the newly created user.</summary>
    public Guid UserId { get; set; }

    /// <summary>The email the new account was registered with.</summary>
    public string Email { get; set; } = string.Empty;
}
