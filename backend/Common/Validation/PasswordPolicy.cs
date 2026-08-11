namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Shared password-length limit. <see cref="Services.PasswordHasher"/> is backed by BCrypt, which
/// silently truncates any input beyond this many UTF-8 bytes rather than rejecting it (confirmed
/// empirically against the installed BCrypt.Net-Next version: bytes 1-72 are significant, byte 73
/// onward has no effect on the resulting hash — and the same truncation applies symmetrically to
/// <c>Verify</c>, so an over-long login password would still match as long as its first 72 bytes
/// do, regardless of what follows). Enforced via <see cref="MaxUtf8BytesAttribute"/> on every
/// Password DTO field (shipper/agency registration and login) and re-checked directly in
/// <c>AuthService.SeedAdminIfNotExistsAsync</c>, which has no DTO/model-validation pass of its own.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>BCrypt's hard input limit, in UTF-8 bytes.</summary>
    public const int MaxBytes = 72;
}
