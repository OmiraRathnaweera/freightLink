namespace FreightLink.Api.Services.Interfaces;

/// <summary>One-way password hashing used by registration, admin seeding, and login verification.</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plain-text password for storage.</summary>
    /// <param name="plainTextPassword">The password to hash.</param>
    /// <returns>A salted, one-way hash suitable for persisting on <c>User.PasswordHash</c>.</returns>
    string Hash(string plainTextPassword);

    /// <summary>Verifies a plain-text password against a previously stored hash.</summary>
    /// <param name="plainTextPassword">The password supplied by the caller.</param>
    /// <param name="passwordHash">The stored hash to check against.</param>
    /// <returns><c>true</c> if the password matches the hash.</returns>
    bool Verify(string plainTextPassword, string passwordHash);
}
