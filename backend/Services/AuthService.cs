using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Email;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Common.Validation;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IAuthService" />
public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly AdminSeedOptions _adminSeedOptions;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<AuthService> _logger;

    /// <summary>Creates the auth service with its DB context, password hasher, token service, and admin-seed settings.</summary>
    public AuthService(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IEmailService emailService,
        IOptions<AdminSeedOptions> adminSeedOptions,
        IOptions<EmailOptions> emailOptions,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailService = emailService;
        _adminSeedOptions = adminSeedOptions.Value;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RegisterResponseDto> RegisterShipperAsync(RegisterShipperRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.EMAIL_ALREADY_REGISTERED, "An account with this email already exists.");
        }

        // BusinessRegNo is optional for shippers (unlike Agency's required one), so this check must
        // be skipped when null — `x.BusinessRegNo == null` would otherwise translate to
        // `WHERE "BusinessRegNo" IS NULL` and false-positive against every other shipper who also
        // omitted theirs, since EF applies C# null-comparison semantics to the translated SQL.
        if (request.BusinessRegNo is not null
            && await _dbContext.ShipperProfiles.AnyAsync(s => s.BusinessRegNo == request.BusinessRegNo, cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, "A shipper with this business registration number already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var verificationToken = CreateAccountToken();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName,
            PhoneE164 = request.PhoneE164,
            IsActive = true,
            EmailVerificationTokenHash = HashAccountToken(verificationToken),
            EmailVerificationTokenExpiresAt = now.AddHours(24),
            CreatedAt = now,
            UpdatedAt = now
        };

        var shipperProfile = new ShipperProfile
        {
            UserId = user.UserId,
            CompanyName = request.CompanyName,
            BusinessRegNo = request.BusinessRegNo,
            BillingAddress = request.BillingAddress,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Users.Add(user);
        _dbContext.ShipperProfiles.Add(shipperProfile);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            // Races against the AnyAsync pre-checks above: two concurrent requests can both pass
            // the pre-check and then collide here. Translate the raw unique-violation into the
            // same ApiException the pre-check would have thrown, instead of an unhandled 500.
            throw MapUniqueViolationToApiException(pg);
        }

        await TrySendEmailVerificationAsync(user, verificationToken, cancellationToken);

        return new RegisterResponseDto
        {
            Message = "Registration successful. Check your email to verify your account before signing in.",
            UserId = user.UserId,
            Email = user.Email
        };
    }

    /// <inheritdoc />
    public async Task<RegisterResponseDto> RegisterAgencyAsync(RegisterAgencyRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.EMAIL_ALREADY_REGISTERED, "An account with this email already exists.");
        }

        if (await _dbContext.Agencies.AnyAsync(a => a.BusinessRegNo == request.BusinessRegNo, cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, "An agency with this business registration number already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var verificationToken = CreateAccountToken();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.AgencyStaff,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName,
            PhoneE164 = request.PhoneE164,
            IsActive = true,
            EmailVerificationTokenHash = HashAccountToken(verificationToken),
            EmailVerificationTokenExpiresAt = now.AddHours(24),
            CreatedAt = now,
            UpdatedAt = now
        };

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = request.AgencyName,
            BusinessRegNo = request.BusinessRegNo,
            YardAddress = request.YardAddress,
            YardLat = request.YardLat,
            YardLng = request.YardLng,
            Status = AgencyStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        var agencyStaff = new AgencyStaff
        {
            UserId = user.UserId,
            AgencyId = agency.AgencyId,
            JobTitle = request.JobTitle,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Users.Add(user);
        _dbContext.Agencies.Add(agency);
        _dbContext.AgencyStaff.Add(agencyStaff);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            // Races against the AnyAsync pre-checks above: two concurrent requests can both pass
            // the pre-check and then collide here. Translate the raw unique-violation into the
            // same ApiException the pre-check would have thrown, instead of an unhandled 500.
            throw MapUniqueViolationToApiException(pg);
        }

        await TrySendEmailVerificationAsync(user, verificationToken, cancellationToken);

        return new RegisterResponseDto
        {
            Message = "Registration successful. Check your email to verify your account before signing in.",
            UserId = user.UserId,
            Email = user.Email
        };
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<List<AgencyLookupDto>> GetAgenciesLookupAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Agencies.AsNoTracking()
            .Where(a => a.Status != AgencyStatus.Suspended)
            .OrderBy(a => a.Name)
            .Select(a => new AgencyLookupDto
            {
                AgencyId = a.AgencyId,
                Name = a.Name
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RequestPasswordResetAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.Email == NormalizeEmail(request.Email), cancellationToken);

        // Always return success to the controller. This endpoint must not disclose whether a
        // particular email address is registered, active, or eligible for login.
        if (user is null || !user.IsActive)
        {
            return;
        }

        var rawToken = CreateAccountToken();
        user.PasswordResetTokenHash = HashAccountToken(rawToken);
        user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TrySendPasswordResetAsync(user, rawToken, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashAccountToken(request.Token);
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.PasswordResetTokenHash == tokenHash
                 && u.PasswordResetTokenExpiresAt != null
                 && u.PasswordResetTokenExpiresAt > now,
            cancellationToken);

        if (user is null)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_OR_EXPIRED_ACCOUNT_TOKEN,
                "This password-reset link is invalid or has expired.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        user.UpdatedAt = now;

        // A reset invalidates every browser/device session, including the requester’s own session.
        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == user.UserId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashAccountToken(request.Token);
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.EmailVerificationTokenHash == tokenHash
                 && u.EmailVerificationTokenExpiresAt != null
                 && u.EmailVerificationTokenExpiresAt > now,
            cancellationToken);

        if (user is null)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_OR_EXPIRED_ACCOUNT_TOKEN,
                "This email-verification link is invalid or has expired.");
        }

        user.EmailVerifiedAt = now;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAt = null;
        user.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResendEmailVerificationAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.Email == NormalizeEmail(request.Email), cancellationToken);

        if (user is null || !user.IsActive || user.EmailVerifiedAt is not null)
        {
            return;
        }

        var rawToken = CreateAccountToken();
        user.EmailVerificationTokenHash = HashAccountToken(rawToken);
        user.EmailVerificationTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TrySendEmailVerificationAsync(user, rawToken, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TokenResponseDto> LoginAsync(LoginRequestDto request, string? userAgent, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.INVALID_CREDENTIALS, "Email or password is incorrect.");
        }

        if (!user.IsActive)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.ACCOUNT_INACTIVE, "This account is inactive.");
        }

        if (_emailOptions.RequireEmailVerification && user.EmailVerifiedAt is null)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.EMAIL_NOT_VERIFIED,
                "Verify your email address before signing in. You can request a new verification email from the sign-in page.");
        }

        return await IssueTokenPairAsync(user, userAgent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TokenResponseDto> RefreshAsync(RefreshRequestDto request, string? userAgent, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _tokenService.ValidateRefreshTokenAsync(request.RefreshToken, cancellationToken);

        // Mirrors the same check LoginAsync applies before issuing tokens — without it, a
        // deactivated account could keep refreshing forever as long as it held a still-valid
        // refresh token, even though /auth/login already rejects it outright. Checked before
        // revoking so the token isn't burned as a side effect of a rejected attempt: if the
        // account is later reactivated, the same (still unexpired) token keeps working.
        if (!refreshToken.User.IsActive)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.ACCOUNT_INACTIVE, "This account is inactive.");
        }

        if (_emailOptions.RequireEmailVerification && refreshToken.User.EmailVerifiedAt is null)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.EMAIL_NOT_VERIFIED,
                "Verify your email address before continuing.");
        }

        // Atomic revoke-old + issue-successor (single transaction, concurrency-checked) — see
        // ITokenService.RotateRefreshTokenAsync. Not routed through IssueTokenPairAsync/
        // IssueRefreshTokenAsync, which each do their own independent SaveChangesAsync and would
        // reopen the same non-atomic-rotation gap this method exists to close.
        var newRawRefreshToken = await _tokenService.RotateRefreshTokenAsync(refreshToken, userAgent, cancellationToken);
        var accessToken = _tokenService.GenerateAccessToken(refreshToken.User);

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRawRefreshToken
        };
    }

    /// <inheritdoc />
    public async Task LogoutAsync(Guid currentUserId, RefreshRequestDto request, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _tokenService.ValidateRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (refreshToken.UserId != currentUserId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.REFRESH_TOKEN_NOT_OWNED, "This refresh token does not belong to the current user.");
        }

        await _tokenService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CurrentUserResponseDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.AgencyStaff)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.USER_NOT_FOUND, "The current user could not be found.");
        }

        return MapToCurrentUserResponse(user);
    }

    /// <inheritdoc />
    public async Task<CurrentUserResponseDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.AgencyStaff)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.USER_NOT_FOUND, "The current user could not be found.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var emailChanged = normalizedEmail != user.Email;

        if (emailChanged && await _dbContext.Users.AnyAsync(u => u.UserId != userId && u.Email == normalizedEmail, cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.EMAIL_ALREADY_REGISTERED, "An account with this email already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        user.FullName = request.FullName;
        user.PhoneE164 = request.PhoneE164;
        user.UpdatedAt = now;

        string? verificationToken = null;
        if (emailChanged)
        {
            user.Email = normalizedEmail;
            user.EmailVerifiedAt = null;
            verificationToken = CreateAccountToken();
            user.EmailVerificationTokenHash = HashAccountToken(verificationToken);
            user.EmailVerificationTokenExpiresAt = now.AddHours(24);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            // Races against the AnyAsync pre-check above: two concurrent requests can both pass
            // the pre-check and then collide here. Translate the raw unique-violation into the
            // same ApiException the pre-check would have thrown, instead of an unhandled 500.
            throw MapUniqueViolationToApiException(pg);
        }

        if (emailChanged && verificationToken is not null)
        {
            await TrySendEmailVerificationAsync(user, verificationToken, cancellationToken);
        }

        return MapToCurrentUserResponse(user);
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.USER_NOT_FOUND, "The current user could not be found.");
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.INCORRECT_CURRENT_PASSWORD, "The current password you entered is incorrect.");
        }

        var now = DateTimeOffset.UtcNow;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = now;

        // A password change invalidates every session, including the requester's own — mirrors
        // ResetPasswordAsync. The caller must sign in again with the new password afterward.
        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == user.UserId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SeedAdminIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_adminSeedOptions.Email) || string.IsNullOrWhiteSpace(_adminSeedOptions.Password))
        {
            _logger.LogWarning("Admin seed skipped: ADMIN_USER_EMAIL / ADMIN_USER_PASSWORD are not configured.");
            return;
        }

        var normalizedEmail = NormalizeEmail(_adminSeedOptions.Email);

        // Unlike the unset-var case above, an invalid-but-set email would otherwise reach the DB
        // insert and fail the ck_user_email_format CHECK — an unhandled exception during this
        // unconditional startup call would crash the whole app on boot. Log and skip instead.
        if (!Regex.IsMatch(normalizedEmail, AuthPatterns.EmailPattern))
        {
            _logger.LogWarning("Admin seed skipped: ADMIN_USER_EMAIL '{Email}' is not a valid email address.", normalizedEmail);
            return;
        }

        // This path has no DTO/model-validation pass, unlike registration/login — so the same
        // byte-length limit the Password DTOs enforce via MaxUtf8BytesAttribute must be checked
        // here explicitly, or a too-long ADMIN_USER_PASSWORD would be silently truncated by BCrypt
        // rather than rejected (see PasswordPolicy.MaxBytes).
        if (Encoding.UTF8.GetByteCount(_adminSeedOptions.Password) > PasswordPolicy.MaxBytes)
        {
            _logger.LogWarning("Admin seed skipped: ADMIN_USER_PASSWORD exceeds {MaxBytes} bytes and would be silently truncated by BCrypt.", PasswordPolicy.MaxBytes);
            return;
        }

        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var admin = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Admin,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(_adminSeedOptions.Password),
            FullName = "System Administrator",
            IsActive = true,
            EmailVerifiedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Users.Add(admin);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded default admin user {Email}.", normalizedEmail);
    }

    /// <summary>Issues a fresh access token and a persisted refresh token for the given user.</summary>
    private async Task<TokenResponseDto> IssueTokenPairAsync(User user, string? userAgent, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = await _tokenService.IssueRefreshTokenAsync(user, userAgent, cancellationToken);

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    /// <summary>Creates a URL-safe, high-entropy one-time account-action token.</summary>
    private static string CreateAccountToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    /// <summary>Stores only a deterministic SHA-256 hash of one-time tokens, never the raw value.</summary>
    private static string HashAccountToken(string rawToken) => Convert.ToBase64String(
        SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private async Task TrySendEmailVerificationAsync(User user, string rawToken, CancellationToken cancellationToken)
    {
        var url = BuildFrontendActionUrl("/verify-email", rawToken);
        var (subject, htmlBody, textBody) = EmailTemplates.BuildEmailVerification(user.FullName, url);
        await TrySendAccountEmailAsync(user.Email, subject, htmlBody, textBody, "email-verification", cancellationToken);
    }

    private async Task TrySendPasswordResetAsync(User user, string rawToken, CancellationToken cancellationToken)
    {
        var url = BuildFrontendActionUrl("/reset-password", rawToken);
        var (subject, htmlBody, textBody) = EmailTemplates.BuildPasswordReset(user.FullName, url);
        await TrySendAccountEmailAsync(user.Email, subject, htmlBody, textBody, "password-reset", cancellationToken);
    }

    private async Task TrySendAccountEmailAsync(
        string email,
        string subject,
        string htmlBody,
        string textBody,
        string templateKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(new EmailMessage
            {
                To = email,
                Subject = subject,
                HtmlBody = htmlBody,
                TextBody = textBody,
                Metadata = new Dictionary<string, string> { ["template"] = templateKey }
            }, cancellationToken);
        }
        catch (ApiException exception) when (exception.Code == ErrorCode.EMAIL_SEND_FAILED)
        {
            // The account action is persisted even if SMTP is temporarily unavailable. Returning
            // success prevents reset/resend endpoints from becoming account-enumeration oracles;
            // the caller can safely request another message later.
            _logger.LogError(exception, "Unable to send {TemplateKey} email to {Email}.", templateKey, email);
        }
    }

    private string BuildFrontendActionUrl(string path, string rawToken)
    {
        var baseUrl = _emailOptions.FrontendBaseUrl.TrimEnd('/');
        return $"{baseUrl}{path}?token={Uri.EscapeDataString(rawToken)}";
    }

    /// <summary>Maps a <see cref="User"/> entity to its safe, wire-facing representation (no password hash).</summary>
    private static CurrentUserResponseDto MapToCurrentUserResponse(User user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        FullName = user.FullName,
        PhoneE164 = user.PhoneE164,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        IsEmailVerified = user.EmailVerifiedAt is not null,
        CreatedAt = user.CreatedAt,
        AgencyId = user.AgencyStaff?.AgencyId
    };

    /// <summary>Normalizes an email for case-insensitive storage/lookup.</summary>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Translates a Postgres unique-violation (23505) caught around a registration
    /// <c>SaveChangesAsync</c> into the same <see cref="ApiException"/> the corresponding
    /// <c>AnyAsync</c> pre-check would have thrown, keyed by the violated constraint's name.
    /// </summary>
    /// <param name="pg">The unique-violation exception raised by Npgsql.</param>
    /// <returns>The domain-appropriate <see cref="ApiException"/> to throw.</returns>
    private static ApiException MapUniqueViolationToApiException(PostgresException pg) => pg.ConstraintName switch
    {
        "uq_user_email" => new ApiException(HttpStatusCode.Conflict, ErrorCode.EMAIL_ALREADY_REGISTERED, "An account with this email already exists."),
        "uq_shipperprofile_regno" => new ApiException(HttpStatusCode.Conflict, ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, "A shipper with this business registration number already exists."),
        "uq_agency_regno" => new ApiException(HttpStatusCode.Conflict, ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, "An agency with this business registration number already exists."),
        _ => throw new DbUpdateException("Unhandled unique-constraint violation.", pg)
    };
}
