using System.Security.Cryptography;
using System.Text;

namespace FreightLink.Api.Common.Security;

/// <summary>
/// Shared one-time-token primitives for every email action link in the system: account
/// tokens (email verification, password reset - see Services/AuthService.cs) and assignment
/// action tokens (email Accept/Decline links - see Services/AssignmentActionTokenService.cs).
/// Only ever persist <see cref="HashToken"/>'s output, never the raw token.
/// </summary>
public static class AccountTokens
{
    /// <summary>Creates a URL-safe, high-entropy one-time token.</summary>
    public static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    /// <summary>Computes the deterministic SHA-256 hash stored in place of the raw token.</summary>
    public static string HashToken(string rawToken) => Convert.ToBase64String(
        SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
