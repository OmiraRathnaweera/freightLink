using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>Payload for consuming a one-time email-verification token.</summary>
public class VerifyEmailRequestDto
{
    [Required]
    [StringLength(256)]
    public string Token { get; set; } = string.Empty;
}
