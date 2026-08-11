using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>
/// Payload for <c>POST /api/v1/auth/register/agency</c> — public, self-service registration
/// that creates a new Agency org and its first Agency Staff user (the caller) atomically.
/// </summary>
public class RegisterAgencyRequestDto
{
    /// <summary>
    /// Login email for the new Agency Staff user; must be unique across all users.
    /// <see cref="EmailAddressAttribute"/> is a loose first-pass check;
    /// <see cref="AuthPatterns.EmailPattern"/> is the exact-match backstop that mirrors the DB's
    /// <c>ck_user_email_format</c> CHECK, so a value that passes here is guaranteed not to fail at
    /// insert time.
    /// </summary>
    [Required]
    [EmailAddress]
    [RegularExpression(AuthPatterns.EmailPattern, ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Account password; must satisfy <see cref="StrongPasswordAttribute"/> and fit within
    /// <see cref="PasswordPolicy.MaxBytes"/> (BCrypt would otherwise silently truncate it).
    /// </summary>
    [Required]
    [StrongPassword]
    [MaxUtf8Bytes(PasswordPolicy.MaxBytes)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Full name of the staff member registering.</summary>
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Optional contact phone number in E.164 format (mandatory leading '+'), matching the DB's
    /// <c>ck_user_phone_e164</c> CHECK exactly via <see cref="AuthPatterns.PhonePattern"/>.
    /// </summary>
    [RegularExpression(AuthPatterns.PhonePattern, ErrorMessage = "Phone number must be a valid E.164 number (e.g. +14155552671).")]
    public string? PhoneE164 { get; set; }

    /// <summary>Optional job title, stored on the new <c>AgencyStaff</c> row.</summary>
    [StringLength(150)]
    public string? JobTitle { get; set; }

    /// <summary>Display name of the new Agency org.</summary>
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string AgencyName { get; set; } = string.Empty;

    /// <summary>Business registration number for the new Agency; must be unique across all agencies.</summary>
    [Required]
    [StringLength(100)]
    public string BusinessRegNo { get; set; } = string.Empty;

    /// <summary>Physical address of the agency's yard.</summary>
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string YardAddress { get; set; } = string.Empty;

    /// <summary>Latitude of the agency's yard, in decimal degrees.</summary>
    [Required]
    [Range(-90, 90)]
    public decimal YardLat { get; set; }

    /// <summary>Longitude of the agency's yard, in decimal degrees.</summary>
    [Required]
    [Range(-180, 180)]
    public decimal YardLng { get; set; }
}
