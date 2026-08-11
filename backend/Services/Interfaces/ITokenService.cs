using FreightLink.Api.Entities;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Issues, validates, and revokes JWT access tokens and hashed refresh tokens.
/// Refresh tokens are single-use: <see cref="AuthService"/> revokes the old token and issues a
/// new one on every <c>/auth/refresh</c> call (rotation), rather than reusing the same token.
/// Rotation itself (<see cref="RotateRefreshTokenAsync"/>) is atomic and single-winner under
/// concurrency — see its doc comment.
/// </summary>
public interface ITokenService
{
    /// <summary>Generates a signed JWT access token carrying the user's id, email, and role.</summary>
    /// <param name="user">The authenticated user to issue a token for.</param>
    /// <returns>The signed, encoded JWT.</returns>
    string GenerateAccessToken(User user);

    /// <summary>Creates and persists a new refresh token (stored hashed) for the given user.</summary>
    /// <param name="user">The user to issue a refresh token for.</param>
    /// <param name="userAgent">Optional client user-agent, recorded for auditing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw (unhashed) refresh token to return to the client — never persisted in this form.</returns>
    Task<string> IssueRefreshTokenAsync(User user, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>Looks up and validates a raw refresh token.</summary>
    /// <param name="rawToken">The raw refresh token supplied by the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching <see cref="RefreshToken"/> entity (with <see cref="RefreshToken.User"/> loaded).</returns>
    /// <exception cref="Common.Exceptions.ApiException">Thrown (401) if the token is unknown, revoked, or expired.</exception>
    Task<RefreshToken> ValidateRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Marks a refresh token as revoked so it can never be used again.</summary>
    /// <param name="refreshToken">The token to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically revokes <paramref name="refreshToken"/> and issues its successor in a single
    /// <c>SaveChangesAsync</c>/transaction, so a crash or failure between the two steps can never
    /// leave the caller with a revoked token and no replacement. Concurrency-safe: if two requests
    /// race to rotate the exact same token, the entity's Postgres <c>xmin</c> concurrency token
    /// (configured in <c>RefreshTokenConfiguration</c>) guarantees only one wins — the loser gets a
    /// clean <see cref="Common.Exceptions.ApiException"/> instead of both silently succeeding.
    /// </summary>
    /// <param name="refreshToken">The already-validated token to consume (from <see cref="ValidateRefreshTokenAsync"/>).</param>
    /// <param name="userAgent">Optional client user-agent, recorded against the newly issued successor token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new raw (unhashed) refresh token.</returns>
    /// <exception cref="Common.Exceptions.ApiException">401 if a concurrent request already consumed this token first.</exception>
    Task<string> RotateRefreshTokenAsync(RefreshToken refreshToken, string? userAgent, CancellationToken cancellationToken = default);
}
