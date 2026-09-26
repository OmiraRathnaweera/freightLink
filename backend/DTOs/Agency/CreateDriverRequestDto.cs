using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Agency;

/// <summary>
/// Payload for onboarding/creating a driver under an agency (<c>POST /api/v1/agencies/{id}/drivers</c>).
/// </summary>
public class CreateDriverRequestDto
{
    /// <summary>
    /// Login email for the new driver user; must be unique across all users.
    /// </summary>
    [Required]
    [EmailAddress]
    [RegularExpression(AuthPatterns.EmailPattern, ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Account password; must satisfy <see cref="StrongPasswordAttribute"/> and fit within
    /// <see cref="PasswordPolicy.MaxBytes"/>.
    /// </summary>
    [Required]
    [StrongPassword]
    [MaxUtf8Bytes(PasswordPolicy.MaxBytes)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Full name of the driver registering.</summary>
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Optional contact phone number in E.164 format (mandatory leading '+'), matching the DB's
    /// <c>ck_user_phone_e164</c> CHECK exactly via <see cref="AuthPatterns.PhonePattern"/>.
    /// </summary>
    [RegularExpression(AuthPatterns.PhonePattern, ErrorMessage = "Phone number must be a valid E.164 number (e.g. +14155552671).")]
    public string? PhoneE164 { get; set; }

    /// <summary>Driver's driving licence number; must be unique across all drivers.</summary>
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string LicenceNo { get; set; } = string.Empty;

    /// <summary>Expiry date of the driving licence; must be in the future.</summary>
    [Required]
    public DateOnly LicenceExpiry { get; set; }
}
