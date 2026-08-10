using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IAuthService" />
public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly AdminSeedOptions _adminSeedOptions;
    private readonly ILogger<AuthService> _logger;

    /// <summary>Creates the auth service with its DB context, password hasher, token service, and admin-seed settings.</summary>
    public AuthService(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<AdminSeedOptions> adminSeedOptions,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _adminSeedOptions = adminSeedOptions.Value;
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

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName,
            PhoneE164 = request.PhoneE164,
            IsActive = true,
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
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterResponseDto
        {
            Message = "Shipper registered successfully.",
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
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.AgencyStaff,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName,
            PhoneE164 = request.PhoneE164,
            IsActive = true,
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
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterResponseDto
        {
            Message = "Agency registered successfully.",
            UserId = user.UserId,
            Email = user.Email
        };
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

        return await IssueTokenPairAsync(user, userAgent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TokenResponseDto> RefreshAsync(RefreshRequestDto request, string? userAgent, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _tokenService.ValidateRefreshTokenAsync(request.RefreshToken, cancellationToken);
        await _tokenService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);

        return await IssueTokenPairAsync(refreshToken.User, userAgent, cancellationToken);
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
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.USER_NOT_FOUND, "The current user could not be found.");
        }

        return MapToCurrentUserResponse(user);
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

    /// <summary>Maps a <see cref="User"/> entity to its safe, wire-facing representation (no password hash).</summary>
    private static CurrentUserResponseDto MapToCurrentUserResponse(User user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        FullName = user.FullName,
        PhoneE164 = user.PhoneE164,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };

    /// <summary>Normalizes an email for case-insensitive storage/lookup.</summary>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
