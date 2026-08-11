using FreightLink.Api.Common.Validation;
using FreightLink.Api.Services.Interfaces;

namespace FreightLink.Api.Services;

/// <summary>
/// BCrypt-backed implementation of <see cref="IPasswordHasher"/>. BCrypt silently truncates any
/// input beyond <see cref="PasswordPolicy.MaxBytes"/> UTF-8 bytes rather than rejecting it — every
/// caller of <see cref="Hash"/>/<see cref="Verify"/> is expected to have already enforced that
/// limit at its own boundary (see <see cref="Common.Validation.MaxUtf8BytesAttribute"/> on the
/// Password DTO fields, and the equivalent check in <c>AuthService.SeedAdminIfNotExistsAsync</c>
/// for the env-var-driven admin seed, which has no DTO validation pass of its own) — this class
/// does not re-check it, since by the time a password reaches here that boundary has already run.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    /// <inheritdoc />
    public string Hash(string plainTextPassword) => BCrypt.Net.BCrypt.HashPassword(plainTextPassword);

    /// <inheritdoc />
    public bool Verify(string plainTextPassword, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(plainTextPassword, passwordHash);
}
