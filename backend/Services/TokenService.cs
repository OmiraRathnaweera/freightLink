using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Data;
using FreightLink.Api.Entities;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="ITokenService" />
public class TokenService : ITokenService
{
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;

    /// <summary>Creates the token service with the DB context (for refresh-token persistence) and JWT settings.</summary>
    public TokenService(AppDbContext dbContext, IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
    }

    /// <inheritdoc />
    public string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public async Task<string> IssueRefreshTokenAsync(User user, string? userAgent, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateRawRefreshToken();
        var now = DateTimeOffset.UtcNow;

        var refreshToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = HashToken(rawToken),
            UserAgent = userAgent,
            IssuedAt = now,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays)
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    /// <inheritdoc />
    public async Task<RefreshToken> ValidateRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(rawToken);

        var refreshToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null || refreshToken.RevokedAt is not null || refreshToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ApiException(System.Net.HttpStatusCode.Unauthorized, ErrorCode.INVALID_REFRESH_TOKEN, "The refresh token is invalid, expired, or has already been revoked.");
        }

        return refreshToken;
    }

    /// <inheritdoc />
    public async Task RevokeRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> RotateRefreshTokenAsync(RefreshToken refreshToken, string? userAgent, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        refreshToken.RevokedAt = now;

        var rawToken = GenerateRawRefreshToken();
        var successor = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = refreshToken.UserId,
            TokenHash = HashToken(rawToken),
            UserAgent = userAgent,
            IssuedAt = now,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays)
        };
        _dbContext.RefreshTokens.Add(successor);

        try
        {
            // Single SaveChangesAsync = single transaction: the revoke and the successor insert
            // either both land or neither does, so a failure here never leaves the caller with a
            // burned token and no replacement.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The xmin concurrency token on RefreshToken (RefreshTokenConfiguration) caught a
            // concurrent rotation of this exact token that committed first — that request wins,
            // this one loses. Nothing from this attempt was persisted.
            throw new ApiException(System.Net.HttpStatusCode.Unauthorized, ErrorCode.INVALID_REFRESH_TOKEN, "The refresh token is invalid, expired, or has already been revoked.");
        }

        return rawToken;
    }

    /// <summary>Generates a cryptographically random, base64url-encoded 256-bit refresh token.</summary>
    private static string GenerateRawRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    /// <summary>SHA-256 hashes a raw refresh token so only the hash is ever persisted, mirroring password hashing.</summary>
    private static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
