using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>Payload for <c>POST /api/v1/auth/register/shipper</c> — public, self-service Shipper registration.</summary>
public class RegisterShipperRequestDto
{
    /// <summary>Login email; must be unique across all users.</summary>
    [Required]
    [EmailAddress]
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

    /// <summary>Optional contact phone number in E.164 format.</summary>
    [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Phone number must be a valid E.164 number.")]
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
