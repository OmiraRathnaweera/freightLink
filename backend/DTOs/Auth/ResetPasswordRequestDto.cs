using System.ComponentModel.DataAnnotations;
using FreightLink.Api.Common.Validation;

namespace FreightLink.Api.DTOs.Auth;

/// <summary>Payload for consuming a one-time password-reset token.</summary>
public class ResetPasswordRequestDto
{
    [Required]
    [StringLength(256)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StrongPassword]
    [MaxUtf8Bytes(PasswordPolicy.MaxBytes)]
    public string NewPassword { get; set; } = string.Empty;
}
