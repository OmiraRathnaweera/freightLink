using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>Payload for <c>POST /api/v1/auth/login</c> — one shared login for every role.</summary>
public class LoginRequestDto
{
    /// <summary>Account email.</summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Account password, in plain text over HTTPS (never logged or persisted as-is). Capped at
    /// <see cref="PasswordPolicy.MaxBytes"/>, not just <see cref="StrongPasswordAttribute"/>'s
    /// creation-time rules — BCrypt's <c>Verify</c> truncates symmetrically with <c>Hash</c>, so
    /// without this an over-long login attempt would still match as long as its first
    /// <see cref="PasswordPolicy.MaxBytes"/> bytes do, regardless of what follows.
    /// </summary>
    [Required]
    [MaxUtf8Bytes(PasswordPolicy.MaxBytes)]
    public string Password { get; set; } = string.Empty;
}
