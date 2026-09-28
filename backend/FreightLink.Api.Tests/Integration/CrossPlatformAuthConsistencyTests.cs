using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Cross-platform consistency integration tests (Y3S01-103) verifying that authentication,
/// JWT tokens, user identity DTOs, and role authorization policies behave identically
/// across both the React (web) and Flutter (mobile) clients.
/// </summary>
public class CrossPlatformAuthConsistencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CrossPlatformAuthConsistencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<(User User, string AccessToken, string RefreshToken)> RegisterShipperAsync()
    {
        var email = $"shipper-cross-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecret1";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = email,
            Password = password,
            FullName = "CrossPlatform Shipper",
            CompanyName = "Acme Global",
            BillingAddress = "100 Port Rd, Colombo"
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });
        loginResponse.EnsureSuccessStatusCode();

        var tokens = (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.First(u => u.Email == email);

        return (user, tokens.AccessToken, tokens.RefreshToken);
    }

    private async Task<(User User, string AccessToken, string RefreshToken)> RegisterAgencyStaffAsync()
    {
        var email = $"agency-cross-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecret1";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register/agency", new RegisterAgencyRequestDto
        {
            Email = email,
            Password = password,
            FullName = "CrossPlatform AgencyStaff",
            AgencyName = "CrossAgency Logistics",
            BusinessRegNo = $"BRN-X-{Guid.NewGuid():N}",
            YardAddress = "200 Logistics Park",
            YardLat = 6.9m,
            YardLng = 79.8m
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });
        loginResponse.EnsureSuccessStatusCode();

        var tokens = (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.First(u => u.Email == email);

        return (user, tokens.AccessToken, tokens.RefreshToken);
    }

    private async Task<(User User, string AccessToken, string RefreshToken)> RegisterDriverAsync()
    {
        var (agencyUser, agencyStaffToken, _) = await RegisterAgencyStaffAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agency = db.Agencies.First(a => a.Staff.Any(s => s.UserId == agencyUser.UserId));

        var driverEmail = $"driver-cross-{Guid.NewGuid():N}@example.com";

        using var addDriverReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/agencies/{agency.AgencyId}/drivers")
        {
            Content = JsonContent.Create(new CreateDriverRequestDto
            {
                Email = driverEmail,
                FullName = "CrossPlatform Driver",
                PhoneE164 = "+94770000000",
                LicenceNo = $"DL-{Guid.NewGuid():N}"[..12],
                LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2))
            })
        };
        addDriverReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agencyStaffToken);
        var registerResponse = await _client.SendAsync(addDriverReq);
        registerResponse.EnsureSuccessStatusCode();
        var createdDriver = await registerResponse.Content.ReadFromJsonAsync<DriverResponseDto>(JsonOpts);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto
        {
            Email = driverEmail,
            Password = createdDriver!.TemporaryPassword!
        });
        loginResponse.EnsureSuccessStatusCode();

        var tokens = (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
        var user = db.Users.First(u => u.Email == driverEmail);

        return (user, tokens.AccessToken, tokens.RefreshToken);
    }

    private async Task<(User User, string AccessToken)> SeedAdminUserAsync()
    {
        var adminUserId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var email = $"admin-cross-{Guid.NewGuid():N}@example.com";

        var adminUser = new User
        {
            UserId = adminUserId,
            Role = UserRole.Admin,
            Email = email,
            PasswordHash = "unused-test-hash",
            FullName = "CrossPlatform Admin",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(adminUser);
            await db.SaveChangesAsync();

            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var accessToken = tokenService.GenerateAccessToken(adminUser);
            return (adminUser, accessToken);
        }
    }

    [Fact]
    public async Task JWTFormatAndClaims_MatchClientExpectations_ForAllRoles()
    {
        var (shipper, shipperToken, _) = await RegisterShipperAsync();
        var (agency, agencyToken, _) = await RegisterAgencyStaffAsync();
        var (driver, driverToken, _) = await RegisterDriverAsync();
        var (admin, adminToken) = await SeedAdminUserAsync();

        var tokenHandler = new JwtSecurityTokenHandler();

        // 1. Shipper JWT
        var shipperJwt = tokenHandler.ReadJwtToken(shipperToken);
        Assert.Equal("HS256", shipperJwt.Header.Alg);
        Assert.Equal(shipper.UserId.ToString(), shipperJwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(shipper.Email, shipperJwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Shipper", shipperJwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);

        // 2. AgencyStaff JWT
        var agencyJwt = tokenHandler.ReadJwtToken(agencyToken);
        Assert.Equal(agency.UserId.ToString(), agencyJwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("AgencyStaff", agencyJwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);

        // 3. Driver JWT
        var driverJwt = tokenHandler.ReadJwtToken(driverToken);
        Assert.Equal(driver.UserId.ToString(), driverJwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("Driver", driverJwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);

        // 4. Admin JWT
        var adminJwt = tokenHandler.ReadJwtToken(adminToken);
        Assert.Equal(admin.UserId.ToString(), adminJwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("Admin", adminJwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task CurrentUserProfileContract_MatchesReactAndFlutterDTOs()
    {
        var (shipper, token, _) = await RegisterShipperAsync();

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/auth/me", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        // Verify JSON wire properties are camelCase as expected by React & Flutter
        Assert.True(root.TryGetProperty("userId", out var userIdProp));
        Assert.Equal(shipper.UserId.ToString(), userIdProp.GetString());

        Assert.True(root.TryGetProperty("email", out var emailProp));
        Assert.Equal(shipper.Email, emailProp.GetString());

        Assert.True(root.TryGetProperty("fullName", out var nameProp));
        Assert.Equal("CrossPlatform Shipper", nameProp.GetString());

        Assert.True(root.TryGetProperty("role", out var roleProp));
        Assert.Equal("Shipper", roleProp.GetString());

        Assert.True(root.TryGetProperty("isActive", out var activeProp));
        Assert.True(activeProp.GetBoolean());

        Assert.True(root.TryGetProperty("createdAt", out _));
    }

    [Fact]
    public async Task RoleAuthorizationBoundaries_EnforceConsistentAccessAcrossEndpoints()
    {
        var (_, shipperToken, _) = await RegisterShipperAsync();
        var (_, agencyToken, _) = await RegisterAgencyStaffAsync();
        var (_, driverToken, _) = await RegisterDriverAsync();
        var (_, adminToken) = await SeedAdminUserAsync();

        // 1. Shipper can read loads, but is forbidden from Admin pricing
        using (var shipperLoadsReq = AuthedRequest(HttpMethod.Get, "/api/v1/loads", shipperToken))
        {
            var res = await _client.SendAsync(shipperLoadsReq);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        using (var shipperAdminPricingReq = AuthedRequest(HttpMethod.Get, "/api/v1/admin/pricing/fuel-rates", shipperToken))
        {
            var res = await _client.SendAsync(shipperAdminPricingReq);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        // 2. Driver can read trips, but is forbidden from creating loads
        using (var driverTripReq = AuthedRequest(HttpMethod.Get, "/api/v1/trips", driverToken))
        {
            var res = await _client.SendAsync(driverTripReq);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        using (var driverPostLoadReq = AuthedRequest(HttpMethod.Post, "/api/v1/loads", driverToken))
        {
            driverPostLoadReq.Content = JsonContent.Create(new { cargoDescription = "Test" });
            var res = await _client.SendAsync(driverPostLoadReq);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        // 3. AgencyStaff can view agency fleet, but is forbidden from creating loads
        using (var agencyFleetReq = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/my/fleet", agencyToken))
        {
            var res = await _client.SendAsync(agencyFleetReq);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        using (var agencyPostLoadReq = AuthedRequest(HttpMethod.Post, "/api/v1/loads", agencyToken))
        {
            agencyPostLoadReq.Content = JsonContent.Create(new { cargoDescription = "Test" });
            var res = await _client.SendAsync(agencyPostLoadReq);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        // 4. Admin has cross-cutting management access (pricing config + loads view)
        using (var adminPricingReq = AuthedRequest(HttpMethod.Get, "/api/v1/admin/pricing/fuel-rates", adminToken))
        {
            var res = await _client.SendAsync(adminPricingReq);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        using (var adminLoadsReq = AuthedRequest(HttpMethod.Get, "/api/v1/loads", adminToken))
        {
            var res = await _client.SendAsync(adminLoadsReq);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }

    [Fact]
    public async Task SessionRevocationAnd401_BehaveConsistently()
    {
        // 1. Unauthenticated request to /auth/me returns 401 with standard UNAUTHORIZED envelope
        using (var anonReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            var res = await _client.SendAsync(anonReq);
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        // 2. Logout revokes refresh token, and subsequent refresh attempt fails with 401 INVALID_REFRESH_TOKEN
        var (_, token, refreshToken) = await RegisterShipperAsync();

        using (var logoutReq = AuthedRequest(HttpMethod.Post, "/api/v1/auth/logout", token))
        {
            logoutReq.Content = JsonContent.Create(new RefreshRequestDto { RefreshToken = refreshToken });
            var logoutRes = await _client.SendAsync(logoutReq);
            Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);
        }

        // Subsequent /auth/refresh with the revoked token must fail
        var refreshRes = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequestDto
        {
            RefreshToken = refreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
        var rawErr = await refreshRes.Content.ReadAsStringAsync();
        using var errDoc = JsonDocument.Parse(rawErr);
        Assert.Equal("INVALID_REFRESH_TOKEN", errDoc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
