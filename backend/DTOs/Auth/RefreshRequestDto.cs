using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>
/// Payload carrying a raw refresh token. Reused for both <c>POST /api/v1/auth/refresh</c>
/// (rotate) and <c>POST /api/v1/auth/logout</c> (revoke) — no separate logout DTO.
/// </summary>
public class RefreshRequestDto
{
    /// <summary>The raw (unhashed) refresh token previously issued to the client.</summary>
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
