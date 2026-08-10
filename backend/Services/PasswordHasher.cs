using FreightLink.Api.Services.Interfaces;

namespace FreightLink.Api.Services;

/// <summary>BCrypt-backed implementation of <see cref="IPasswordHasher"/>.</summary>
public class PasswordHasher : IPasswordHasher
{
    /// <inheritdoc />
    public string Hash(string plainTextPassword) => BCrypt.Net.BCrypt.HashPassword(plainTextPassword);

    /// <inheritdoc />
    public bool Verify(string plainTextPassword, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(plainTextPassword, passwordHash);
}
