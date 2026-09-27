using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FreightLink.Api.DTOs.Analytics;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Loads;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-HTTP-pipeline tests for <c>AdminAnalyticsController</c>, focused on the Admin-only role gate
/// and the summary reflecting real seeded data. Each test builds its own short-lived
/// <see cref="CustomWebApplicationFactory"/> (own InMemory database), since counts must be exact and
/// this data isn't scoped per-owner the way Loads' single-shipper tests can share one database.
/// Arithmetic/grouping coverage lives in <c>AnalyticsServiceTests</c>.
/// </summary>
public class AdminAnalyticsControllerTests
{
    private static async Task<TokenResponseDto> RegisterAndLoginShipperAsync(HttpClient client, string emailPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecret1";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = email,
            Password = password,
            FullName = "Integration Tester",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    /// <summary>
    /// Mints a validly-signed Admin-role JWT with a random, non-existent user id — safe here since
    /// nothing this controller reads stores an Admin id as a foreign key (mirrors
    /// <c>LoadsControllerTests.MintAdminToken</c>).
    /// </summary>
    private static string MintAdminToken()
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-that-is-long-enough-1234567890"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static CreateLoadDto ValidCreateLoadDto() => new()
    {
        CargoDescription = "Pallets of canned goods",
        WeightKg = 500m,
        VolumeM3 = 2.5m,
        PickupAddress = "123 Pickup Street, Colombo",
        PickupLat = 6.9271m,
        PickupLng = 79.8612m,
        DropoffAddress = "456 Dropoff Road, Kandy",
        DropoffLat = 7.2906m,
        DropoffLng = 80.6337m,
        PickupWindowStart = DateTimeOffset.UtcNow.AddDays(1),
        PickupWindowEnd = DateTimeOffset.UtcNow.AddDays(2)
    };

    [Fact]
    public async Task GetSummary_Returns401_WithoutToken()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/analytics/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_Returns403_ForShipper()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var tokens = await RegisterAndLoginShipperAsync(client, "analytics-shipper");

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/admin/analytics/summary", tokens.AccessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Two Draft loads created by two different shippers show up as a total of 2, both Draft.</summary>
    [Fact]
    public async Task GetSummary_Returns200_ForAdmin_ReflectingSeededLoads()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var firstShipper = await RegisterAndLoginShipperAsync(client, "analytics-owner-1");
        using (var createFirst = AuthedRequest(HttpMethod.Post, "/api/v1/loads", firstShipper.AccessToken))
        {
            createFirst.Content = JsonContent.Create(ValidCreateLoadDto());
            (await client.SendAsync(createFirst)).EnsureSuccessStatusCode();
        }

        var secondShipper = await RegisterAndLoginShipperAsync(client, "analytics-owner-2");
        using (var createSecond = AuthedRequest(HttpMethod.Post, "/api/v1/loads", secondShipper.AccessToken))
        {
            createSecond.Content = JsonContent.Create(ValidCreateLoadDto());
            (await client.SendAsync(createSecond)).EnsureSuccessStatusCode();
        }

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/admin/analytics/summary", MintAdminToken());
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AdminAnalyticsSummaryDto>())!;
        Assert.Equal(2, body.Loads.Total);
        var draftBucket = Assert.Single(body.Loads.ByLabel);
        Assert.Equal("Draft", draftBucket.Label);
        Assert.Equal(2, draftBucket.Count);

        // Both registered shippers plus the factory's own seeded users (if any) count toward UsersByRole.
        var shipperBucket = Assert.Single(body.UsersByRole.ByLabel, l => l.Label == "Shipper");
        Assert.True(shipperBucket.Count >= 2);
    }

    /// <summary>
    /// A freshly-created factory has no Loads/Trips/Assignments/Disputes/Invoices (nothing seeds
    /// those), but Program.cs's own startup seeding (<c>AgencyService.SeedDefaultAgenciesIfNotExistsAsync</c>,
    /// which runs unconditionally, even in the "Production" environment this factory forces) always
    /// creates exactly 6 Active agencies — so this asserts the categories with genuinely no seed data
    /// are zero, and the one category that does have baseline seed data reflects it exactly.
    /// </summary>
    [Fact]
    public async Task GetSummary_Returns200_ReflectingBaselineAgencySeed_AndZeroElsewhere()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/admin/analytics/summary", MintAdminToken());
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AdminAnalyticsSummaryDto>())!;
        Assert.Equal(0, body.Loads.Total);
        Assert.Equal(0, body.Trips.Total);
        Assert.Equal(0, body.Assignments.Total);
        Assert.Equal(0, body.Disputes.Total);
        Assert.Equal(0, body.Invoices.Counts.Total);
        Assert.Equal(0m, body.Invoices.TotalInvoicedAmount);
        Assert.Equal(0m, body.Invoices.TotalPaidAmount);

        Assert.Equal(6, body.Agencies.Total);
        var activeBucket = Assert.Single(body.Agencies.ByLabel);
        Assert.Equal("Active", activeBucket.Label);
        Assert.Equal(6, activeBucket.Count);
    }
}
