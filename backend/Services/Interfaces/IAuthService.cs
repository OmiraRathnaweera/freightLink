using FreightLink.Api.DTOs.Auth;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// All authentication and account-lifecycle business logic: registration, login, token
/// refresh/logout, the current-user lookup, and startup admin seeding. Consumed only by
/// <see cref="Controllers.AuthController"/>, which stays thin and delegates everything here.
/// </summary>
public interface IAuthService
{
    /// <summary>Registers a new Shipper: creates the <c>User</c> row and its <c>ShipperProfile</c> atomically.</summary>
    /// <param name="request">Validated shipper registration payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A success message with the new user's id and email — never tokens or a full profile.</returns>
    /// <exception cref="Common.Exceptions.ApiException">409 if the email is already registered.</exception>
    Task<RegisterResponseDto> RegisterShipperAsync(RegisterShipperRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new Agency: creates the <c>User</c>, a brand-new <c>Agency</c> (status <c>Pending</c>),
    /// and the linking <c>AgencyStaff</c> row atomically, since <c>AgencyStaff.AgencyId</c> is a required FK.
    /// </summary>
    /// <param name="request">Validated agency registration payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A success message with the new user's id and email — never tokens or a full profile.</returns>
    /// <exception cref="Common.Exceptions.ApiException">409 if the email or business registration number is already registered.</exception>
    Task<RegisterResponseDto> RegisterAgencyAsync(RegisterAgencyRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Requests a password-reset email without revealing whether the address belongs to an account.</summary>
    Task RequestPasswordResetAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Consumes a one-time reset token, changes the password, and revokes all active sessions.</summary>
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Consumes a one-time email-verification token.</summary>
    Task VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Resends an email-verification message without revealing account existence.</summary>
    Task ResendEmailVerificationAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a list of active agencies available for driver registration selection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of agency lookup items.</returns>
    Task<List<AgencyLookupDto>> GetAgenciesLookupAsync(CancellationToken cancellationToken = default);

    /// <summary>Authenticates a user by email/password, shared across every role.</summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="userAgent">Optional client user-agent, recorded against the issued refresh token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Only the access and refresh tokens — never the user profile.</returns>
    /// <exception cref="Common.Exceptions.ApiException">401 for unknown email/wrong password, 403 if the account is inactive.</exception>
    Task<TokenResponseDto> LoginAsync(LoginRequestDto request, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>Rotates a refresh token: validates and revokes the old one, then issues a new access/refresh pair.</summary>
    /// <param name="request">The raw refresh token to rotate.</param>
    /// <param name="userAgent">Optional client user-agent, recorded against the newly issued refresh token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new token pair. The old refresh token is unusable afterward.</returns>
    /// <exception cref="Common.Exceptions.ApiException">401 if the refresh token is invalid, expired, or already revoked.</exception>
    Task<TokenResponseDto> RefreshAsync(RefreshRequestDto request, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>Revokes a refresh token belonging to the authenticated caller.</summary>
    /// <param name="currentUserId">The id of the authenticated caller, from the access token claims.</param>
    /// <param name="request">The raw refresh token to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="Common.Exceptions.ApiException">401 if the token is invalid; 403 if it doesn't belong to <paramref name="currentUserId"/>.</exception>
    Task LogoutAsync(Guid currentUserId, RefreshRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Loads the authenticated caller's safe profile (no password hash) for <c>GET /auth/me</c>.</summary>
    /// <param name="userId">The id of the authenticated caller, from the access token claims.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current user's safe profile.</returns>
    Task<CurrentUserResponseDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the default Admin user from <c>ADMIN_USER_EMAIL</c>/<c>ADMIN_USER_PASSWORD</c> if one
    /// doesn't already exist. Called once on every app startup; no-ops safely if those env vars are unset.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SeedAdminIfNotExistsAsync(CancellationToken cancellationToken = default);
}
