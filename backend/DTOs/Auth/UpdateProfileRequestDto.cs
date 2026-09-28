using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>
/// Payload for <c>PATCH /api/v1/auth/me</c> — lets any authenticated user update their own
/// account's name, email, and/or phone number. Never touches the password; see
/// <see cref="ChangePasswordRequestDto"/> for that.
/// </summary>
public class UpdateProfileRequestDto
{
    /// <summary>Full name to display for this account.</summary>
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// New login email; must remain unique across all users. Changing it resets email
    /// verification, mirroring registration's own initial unverified state.
    /// </summary>
    [Required]
    [EmailAddress]
    [RegularExpression(AuthPatterns.EmailPattern, ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Optional contact phone number in E.164 format (mandatory leading '+').</summary>
    [RegularExpression(AuthPatterns.PhonePattern, ErrorMessage = "Phone number must be a valid E.164 number (e.g. +14155552671).")]
    public string? PhoneE164 { get; set; }
}
