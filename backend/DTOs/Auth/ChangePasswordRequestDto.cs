using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>
/// Payload for <c>POST /api/v1/auth/change-password</c> — lets an authenticated user change
/// their own password by re-confirming the current one. Unlike <see cref="ResetPasswordRequestDto"/>
/// (unauthenticated, token-based), this requires an active session and the current password.
/// </summary>
public class ChangePasswordRequestDto
{
    /// <summary>The account's current password, re-confirmed as proof of intent.</summary>
    [Required]
    [MaxUtf8Bytes(PasswordPolicy.MaxBytes)]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// The new password; must satisfy <see cref="StrongPasswordAttribute"/> and fit within
    /// <see cref="PasswordPolicy.MaxBytes"/> (BCrypt would otherwise silently truncate it).
    /// </summary>
    [Required]
    [StrongPassword]
    [MaxUtf8Bytes(PasswordPolicy.MaxBytes)]
    public string NewPassword { get; set; } = string.Empty;
}
