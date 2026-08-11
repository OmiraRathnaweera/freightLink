using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AuthService"/> covering registration, login, refresh rotation,
/// logout, and admin seeding. Backed by EF Core's InMemory provider — no real Postgres needed.
/// </summary>
public class AuthServiceTests
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

    /// <summary>Builds a real <see cref="AuthService"/> wired to the given DB context and admin-seed options.</summary>
    private static AuthService CreateSut(AppDbContext dbContext, AdminSeedOptions? adminSeedOptions = null)
    {
        var passwordHasher = new PasswordHasher();
        var tokenService = new TokenService(dbContext, Options.Create(CreateJwtOptions()));
        return new AuthService(
            dbContext,
            passwordHasher,
            tokenService,
            Options.Create(adminSeedOptions ?? new AdminSeedOptions { Email = "admin@freightlink.test", Password = "Adm1n$trongPass!" }),
            NullLogger<AuthService>.Instance);
    }

    /// <summary>A valid Shipper registration payload, with an overridable email/business reg no for uniqueness tests.</summary>
    private static RegisterShipperRequestDto ValidShipperRequest(string email = "shipper@example.com", string? businessRegNo = null) => new()
    {
        Email = email,
        Password = "Sup3r$ecret1",
        FullName = "Jane Shipper",
        CompanyName = "Acme Freight",
        BusinessRegNo = businessRegNo,
        BillingAddress = "123 Main Street, Colombo"
    };

    /// <summary>A valid Agency registration payload, with overridable email/business reg no for uniqueness tests.</summary>
    private static RegisterAgencyRequestDto ValidAgencyRequest(string email = "agency@example.com", string businessRegNo = "BRN-0001") => new()
    {
        Email = email,
        Password = "Sup3r$ecret1",
        FullName = "Alex Agency",
        AgencyName = "Speedy Transport",
        BusinessRegNo = businessRegNo,
        YardAddress = "456 Yard Road, Kandy",
        YardLat = 7.2906m,
        YardLng = 80.6337m
    };

    // --- Registration ---

    /// <summary>Registering a shipper creates both the User and ShipperProfile rows.</summary>
    [Fact]
    public async Task RegisterShipperAsync_Succeeds_WithValidData()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var result = await sut.RegisterShipperAsync(ValidShipperRequest());

        Assert.NotEqual(Guid.Empty, result.UserId);
        var user = await dbContext.Users.FindAsync(result.UserId);
        Assert.NotNull(user);
        Assert.Equal(UserRole.Shipper, user!.Role);
        var profile = await dbContext.ShipperProfiles.FindAsync(result.UserId);
        Assert.NotNull(profile);
    }

    /// <summary>Registering an agency creates the User, a new Agency (Pending), and the AgencyStaff link.</summary>
    [Fact]
    public async Task RegisterAgencyAsync_Succeeds_WithValidData()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var result = await sut.RegisterAgencyAsync(ValidAgencyRequest());

        var user = await dbContext.Users.FindAsync(result.UserId);
        Assert.NotNull(user);
        Assert.Equal(UserRole.AgencyStaff, user!.Role);
        var agencyStaff = await dbContext.AgencyStaff.FindAsync(result.UserId);
        Assert.NotNull(agencyStaff);
        var agency = await dbContext.Agencies.FindAsync(agencyStaff!.AgencyId);
        Assert.NotNull(agency);
        Assert.Equal(AgencyStatus.Pending, agency!.Status);
    }

    /// <summary>Registering a shipper with an already-used email throws EMAIL_ALREADY_REGISTERED.</summary>
    [Fact]
    public async Task RegisterShipperAsync_Throws_ForDuplicateEmail()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        await sut.RegisterShipperAsync(ValidShipperRequest("dup@example.com"));

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.RegisterShipperAsync(ValidShipperRequest("dup@example.com")));
        Assert.Equal(ErrorCode.EMAIL_ALREADY_REGISTERED, exception.Code);
    }

    /// <summary>Registering a shipper with an already-used business reg no throws BUSINESS_REG_NO_ALREADY_REGISTERED
    /// instead of letting the DB's uq_shipperprofile_regno violation surface as an unhandled 500.</summary>
    [Fact]
    public async Task RegisterShipperAsync_Throws_ForDuplicateBusinessRegNo()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        await sut.RegisterShipperAsync(ValidShipperRequest("first@example.com", "BRN-0001"));

        var exception = await Assert.ThrowsAsync<ApiException>(
            () => sut.RegisterShipperAsync(ValidShipperRequest("second@example.com", "BRN-0001")));
        Assert.Equal(ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, exception.Code);
    }

    /// <summary>Business reg no is optional: multiple shippers omitting it must all succeed. Regression test for
    /// a null-semantics bug where a naive `x.BusinessRegNo == request.BusinessRegNo` pre-check would translate
    /// to "BusinessRegNo IS NULL" against a relational provider and false-positive against any other shipper
    /// who also omitted theirs.</summary>
    [Fact]
    public async Task RegisterShipperAsync_Succeeds_WhenBusinessRegNoOmitted_ForMultipleShippers()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        await sut.RegisterShipperAsync(ValidShipperRequest("first@example.com"));
        var result = await sut.RegisterShipperAsync(ValidShipperRequest("second@example.com"));

        Assert.NotEqual(Guid.Empty, result.UserId);
    }

    /// <summary>The persisted password hash differs from the plain-text password and verifies via BCrypt.</summary>
    [Fact]
    public async Task RegisterShipperAsync_StoresHashedPassword_NotPlainText()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var request = ValidShipperRequest();

        var result = await sut.RegisterShipperAsync(request);

        var user = await dbContext.Users.FindAsync(result.UserId);
        Assert.NotEqual(request.Password, user!.PasswordHash);
        Assert.True(new PasswordHasher().Verify(request.Password, user.PasswordHash));
    }

    // --- Login ---

    /// <summary>Logging in with correct credentials returns a non-empty access and refresh token.</summary>
    [Fact]
    public async Task LoginAsync_Succeeds_WithValidCredentials()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var request = ValidShipperRequest();
        await sut.RegisterShipperAsync(request);

        var tokens = await sut.LoginAsync(new LoginRequestDto { Email = request.Email, Password = request.Password }, "test-agent");

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
    }

    /// <summary>Logging in with the wrong password throws INVALID_CREDENTIALS.</summary>
    [Fact]
    public async Task LoginAsync_Throws_ForWrongPassword()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var request = ValidShipperRequest();
        await sut.RegisterShipperAsync(request);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.LoginAsync(new LoginRequestDto { Email = request.Email, Password = "WrongPassword1!" }, null));
        Assert.Equal(ErrorCode.INVALID_CREDENTIALS, exception.Code);
    }

    /// <summary>Logging in with an unknown email throws the same INVALID_CREDENTIALS error as a wrong password (no user-enumeration signal).</summary>
    [Fact]
    public async Task LoginAsync_Throws_ForNonExistentEmail_WithSameErrorAsWrongPassword()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.LoginAsync(new LoginRequestDto { Email = "nobody@example.com", Password = "WhoAreYou1!" }, null));
        Assert.Equal(ErrorCode.INVALID_CREDENTIALS, exception.Code);
    }

    /// <summary>Logging in as a deactivated user throws ACCOUNT_INACTIVE.</summary>
    [Fact]
    public async Task LoginAsync_Throws_ForInactiveUser()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var request = ValidShipperRequest();
        var registerResult = await sut.RegisterShipperAsync(request);

        var user = await dbContext.Users.FindAsync(registerResult.UserId);
        user!.IsActive = false;
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.LoginAsync(new LoginRequestDto { Email = request.Email, Password = request.Password }, null));
        Assert.Equal(ErrorCode.ACCOUNT_INACTIVE, exception.Code);
    }

    // --- Refresh ---

    /// <summary>Refreshing with a valid token returns a brand-new access and refresh token pair.</summary>
    [Fact]
    public async Task RefreshAsync_IssuesNewTokenPair_ForValidToken()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var request = ValidShipperRequest();
        await sut.RegisterShipperAsync(request);
        var loginResult = await sut.LoginAsync(new LoginRequestDto { Email = request.Email, Password = request.Password }, null);

        var refreshed = await sut.RefreshAsync(new RefreshRequestDto { RefreshToken = loginResult.RefreshToken }, null);

        Assert.NotEqual(loginResult.RefreshToken, refreshed.RefreshToken);
        Assert.NotEqual(loginResult.AccessToken, refreshed.AccessToken);
    }

    /// <summary>Refreshing with a token that was never issued throws.</summary>
    [Fact]
    public async Task RefreshAsync_Throws_ForInvalidToken()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        await Assert.ThrowsAsync<ApiException>(() =>
            sut.RefreshAsync(new RefreshRequestDto { RefreshToken = "garbage-token" }, null));
    }

    /// <summary>After one refresh, the old refresh token is revoked and can no longer be used (rotation).</summary>
    [Fact]
    public async Task RefreshAsync_RotatesToken_OldTokenBecomesInvalid()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var request = ValidShipperRequest();
        await sut.RegisterShipperAsync(request);
        var loginResult = await sut.LoginAsync(new LoginRequestDto { Email = request.Email, Password = request.Password }, null);

        await sut.RefreshAsync(new RefreshRequestDto { RefreshToken = loginResult.RefreshToken }, null);

        await Assert.ThrowsAsync<ApiException>(() =>
            sut.RefreshAsync(new RefreshRequestDto { RefreshToken = loginResult.RefreshToken }, null));
    }

    // --- Logout ---

    /// <summary>Logging out revokes the given refresh token so it can never be refreshed with again.</summary>
    [Fact]
    public async Task LogoutAsync_RevokesToken()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var request = ValidShipperRequest();
        var registerResult = await sut.RegisterShipperAsync(request);
        var loginResult = await sut.LoginAsync(new LoginRequestDto { Email = request.Email, Password = request.Password }, null);

        await sut.LogoutAsync(registerResult.UserId, new RefreshRequestDto { RefreshToken = loginResult.RefreshToken });

        await Assert.ThrowsAsync<ApiException>(() =>
            sut.RefreshAsync(new RefreshRequestDto { RefreshToken = loginResult.RefreshToken }, null));
    }

    // --- Admin seed ---

    /// <summary>Seeding creates a Users row with role Admin when none exists yet.</summary>
    [Fact]
    public async Task SeedAdminIfNotExistsAsync_CreatesAdmin_WhenNotExists()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        await sut.SeedAdminIfNotExistsAsync();

        var admin = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == "admin@freightlink.test");
        Assert.NotNull(admin);
        Assert.Equal(UserRole.Admin, admin!.Role);
    }

    /// <summary>Seeding twice does not create a second admin row.</summary>
    [Fact]
    public async Task SeedAdminIfNotExistsAsync_DoesNotDuplicate_WhenAlreadySeeded()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        await sut.SeedAdminIfNotExistsAsync();
        await sut.SeedAdminIfNotExistsAsync();

        var adminCount = await dbContext.Users.CountAsync(u => u.Email == "admin@freightlink.test");
        Assert.Equal(1, adminCount);
    }

    /// <summary>The seeded admin's password is hashed, never stored as plain text.</summary>
    [Fact]
    public async Task SeedAdminIfNotExistsAsync_PasswordIsHashed()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        await sut.SeedAdminIfNotExistsAsync();

        var admin = await dbContext.Users.SingleAsync(u => u.Email == "admin@freightlink.test");
        Assert.NotEqual("Adm1n$trongPass!", admin.PasswordHash);
        Assert.True(new PasswordHasher().Verify("Adm1n$trongPass!", admin.PasswordHash));
    }

    /// <summary>The seeded admin can log in through the same shared login endpoint as any other role.</summary>
    [Fact]
    public async Task SeedAdminIfNotExistsAsync_SeededAdminCanLogin()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        await sut.SeedAdminIfNotExistsAsync();

        var tokens = await sut.LoginAsync(new LoginRequestDto { Email = "admin@freightlink.test", Password = "Adm1n$trongPass!" }, null);

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
    }

    /// <summary>
    /// A malformed ADMIN_USER_EMAIL (would fail the DB's ck_user_email_format CHECK) must be
    /// rejected before the insert, not left to throw and crash startup — mirrors the existing
    /// unset-var no-op via a log-and-skip instead of an unhandled exception.
    /// </summary>
    [Fact]
    public async Task SeedAdminIfNotExistsAsync_SkipsSeed_WhenEmailIsMalformed()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext, new AdminSeedOptions { Email = "not-an-email", Password = "Adm1n$trongPass!" });

        await sut.SeedAdminIfNotExistsAsync();

        Assert.Empty(dbContext.Users);
    }
}
