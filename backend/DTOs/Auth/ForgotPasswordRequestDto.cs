using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>Payload for requesting a password-reset email. Responses are deliberately non-enumerating.</summary>
public class ForgotPasswordRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
}
