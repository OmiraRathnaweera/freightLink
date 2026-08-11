namespace FreightLink.Api.Common.Options;

/// <summary>
/// Credentials for the default Admin account seeded on startup, read from the flat
/// <c>ADMIN_USER_EMAIL</c> / <c>ADMIN_USER_PASSWORD</c> env vars (no public admin registration exists).
/// </summary>
public class AdminSeedOptions
{
    /// <summary>Email of the admin account to seed. Seeding no-ops if unset.</summary>
    public string? Email { get; set; }

    /// <summary>Plain-text password to hash and store for the seeded admin. Seeding no-ops if unset.</summary>
    public string? Password { get; set; }
}
