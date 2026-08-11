using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>Payload for <c>POST /api/v1/auth/register/shipper</c> — public, self-service Shipper registration.</summary>
public class RegisterShipperRequestDto
{
    /// <summary>
    /// Login email; must be unique across all users. <see cref="EmailAddressAttribute"/> is a loose
    /// first-pass check; <see cref="AuthPatterns.EmailPattern"/> is the exact-match backstop that
    /// mirrors the DB's <c>ck_user_email_format</c> CHECK, so a value that passes here is guaranteed
    /// not to fail at insert time.
    /// </summary>
    [Required]
    [EmailAddress]
    [RegularExpression(AuthPatterns.EmailPattern, ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Account password; must satisfy <see cref="StrongPasswordAttribute"/>.</summary>
    [Required]
    [StrongPassword]
    public string Password { get; set; } = string.Empty;

    /// <summary>Full name of the person registering.</summary>
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Optional contact phone number in E.164 format (mandatory leading '+'), matching the DB's
    /// <c>ck_user_phone_e164</c> CHECK exactly via <see cref="AuthPatterns.PhonePattern"/>.
    /// </summary>
    [RegularExpression(AuthPatterns.PhonePattern, ErrorMessage = "Phone number must be a valid E.164 number (e.g. +14155552671).")]
    public string? PhoneE164 { get; set; }

    /// <summary>Name of the shipping company, stored on the new <c>ShipperProfile</c>.</summary>
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Optional business registration number for the shipping company.</summary>
    [StringLength(100)]
    public string? BusinessRegNo { get; set; }

    /// <summary>Billing address for invoices.</summary>
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string BillingAddress { get; set; } = string.Empty;
}
