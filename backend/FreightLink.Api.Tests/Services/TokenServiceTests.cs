using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Data;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TokenService"/>: access-token claims, refresh-token issue/validate
/// round-tripping, and revoked/expired refresh-token rejection. Backed by EF Core's InMemory provider.
/// </summary>
public class TokenServiceTests
{
    /// <summary>Creates a fresh, isolated InMemory-backed <see cref="AppDbContext"/> for one test.</summary>
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Deterministic JWT settings for tests (fixed signing key, 15 min / 7 day lifetimes).</summary>
    private static JwtOptions CreateJwtOptions() => new()
    {
        Issuer = "FreightLinkApi",
        Audience = "FreightLinkClient",
        Key = "unit-test-signing-key-that-is-long-enough-1234567890",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    };

    /// <summary>A minimal, valid Shipper user for token-generation tests.</summary>
    private static User CreateUser() => new()
    {
        UserId = Guid.NewGuid(),
        Role = UserRole.Shipper,
        Email = "shipper@example.com",
        PasswordHash = "hash",
        FullName = "Test Shipper",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>Builds the system under test with the given DB context and JWT options (or the defaults).</summary>
    private static TokenService CreateSut(AppDbContext dbContext, JwtOptions? jwtOptions = null) =>
        new(dbContext, Options.Create(jwtOptions ?? CreateJwtOptions()));

    /// <summary>The generated access token carries the user's id, email, role, and a future expiry.</summary>
    [Fact]
    public void GenerateAccessToken_ContainsExpectedClaims()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var user = CreateUser();

        var accessToken = sut.GenerateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        Assert.Equal(user.UserId.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.Role.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }

    /// <summary>A freshly issued refresh token validates successfully and is not revoked.</summary>
    [Fact]
    public async Task IssueRefreshTokenAsync_Then_ValidateRefreshTokenAsync_RoundTrips()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var user = CreateUser();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var rawToken = await sut.IssueRefreshTokenAsync(user, "test-agent");
        var validated = await sut.ValidateRefreshTokenAsync(rawToken);

        Assert.Equal(user.UserId, validated.UserId);
        Assert.Null(validated.RevokedAt);
    }

    /// <summary>Validating a token that was never issued throws INVALID_REFRESH_TOKEN.</summary>
    [Fact]
    public async Task ValidateRefreshTokenAsync_Throws_ForUnknownToken()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.ValidateRefreshTokenAsync("not-a-real-token"));
        Assert.Equal(ErrorCode.INVALID_REFRESH_TOKEN, exception.Code);
    }

    /// <summary>Once a refresh token is revoked, it can never be validated again.</summary>
    [Fact]
    public async Task ValidateRefreshTokenAsync_Throws_AfterRevoke()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var user = CreateUser();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var rawToken = await sut.IssueRefreshTokenAsync(user, null);
        var refreshToken = await sut.ValidateRefreshTokenAsync(rawToken);

        await sut.RevokeRefreshTokenAsync(refreshToken);

        await Assert.ThrowsAsync<ApiException>(() => sut.ValidateRefreshTokenAsync(rawToken));
    }

    /// <summary>A refresh token past its ExpiresAt is rejected even though it was never revoked.</summary>
    [Fact]
    public async Task ValidateRefreshTokenAsync_Throws_ForExpiredToken()
    {
        using var dbContext = CreateContext();
        var user = CreateUser();
        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("expired-raw-token"))),
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-10),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-3)
        });
        await dbContext.SaveChangesAsync();

        var sut = CreateSut(dbContext);

        await Assert.ThrowsAsync<ApiException>(() => sut.ValidateRefreshTokenAsync("expired-raw-token"));
    }
}
