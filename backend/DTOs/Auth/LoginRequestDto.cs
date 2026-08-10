using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>Payload for <c>POST /api/v1/auth/login</c> — one shared login for every role.</summary>
public class LoginRequestDto
{
    /// <summary>Account email.</summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Account password, in plain text over HTTPS (never logged or persisted as-is).</summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}
