namespace FreightLink.Api.DTOs.Auth;

/// <summary>Generic success response used by non-enumerating account-recovery endpoints.</summary>
public class AccountActionResponseDto
{
    public string Message { get; set; } = string.Empty;
}
