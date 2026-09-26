using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.DTOs.Auth;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

public class AgenciesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AgenciesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<TokenResponseDto> RegisterAndLoginAgencyAsync(string emailPrefix, string regNoPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        var regNo = $"{regNoPrefix}-{Guid.NewGuid():N}";
        const string password = "Sup3r$ecret1";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register/agency", new RegisterAgencyRequestDto
        {
            Email = email,
            Password = password,
            FullName = "Integration Tester",
            AgencyName = "Test Agency",
            BusinessRegNo = regNo,
            YardAddress = "123 Main Street",
            YardLat = 1.0m,
            YardLng = 1.0m
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task GetAllAgencies_Returns200_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/agencies", MintAdminToken());
        var response = await _client.SendAsync(request);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllAgencies_Returns403_ForAgencyStaff()
    {
        var tokens = await RegisterAndLoginAgencyAsync("list-fail", "FAIL");
        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/agencies", tokens.AccessToken);
        
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAgency_Returns403_ForAgencyStaff_OnUnownedAgency()
    {
        var agency1 = await RegisterAndLoginAgencyAsync("update-1", "U1");
        var agency2 = await RegisterAndLoginAgencyAsync("update-2", "U2");

        // Admin gets the ID of agency 2
        using var listReq = AuthedRequest(HttpMethod.Get, "/api/v1/agencies", MintAdminToken());
        var listRes = await _client.SendAsync(listReq);
        var page = await listRes.Content.ReadFromJsonAsync<PagedAgencyResponseDto>();
        var targetAgencyId = page!.Items.Last().AgencyId;

        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{targetAgencyId}", agency1.AccessToken);
        request.Content = JsonContent.Create(new AgencyUpdateDto { Name = "Hacked Name" });
        
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddDriver_Returns201_ForAgencyStaff()
    {
        var agency = await RegisterAndLoginAgencyAsync("add-driver", "DRV-ADD");

        // Fetch agency id via /my/fleet
        using var fleetReq = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/my/fleet", agency.AccessToken);
        var fleetRes = await _client.SendAsync(fleetReq);
        fleetRes.EnsureSuccessStatusCode();
        var fleet = await fleetRes.Content.ReadFromJsonAsync<AgencyFleetResponseDto>();
        var agencyId = fleet!.AgencyId;

        // Add driver
        var driverEmail = $"staff-driver-{Guid.NewGuid():N}@example.com";
        var licenceNo = $"DL-{Guid.NewGuid():N}".Substring(0, 15);
        using var addReq = AuthedRequest(HttpMethod.Post, $"/api/v1/agencies/{agencyId}/drivers", agency.AccessToken);
        addReq.Content = JsonContent.Create(new CreateDriverRequestDto
        {
            Email = driverEmail,
            Password = "Sup3r$ecret1",
            FullName = "Employed Driver",
            PhoneE164 = "+94771234567",
            LicenceNo = licenceNo,
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2))
        });

        var addRes = await _client.SendAsync(addReq);
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);
        var created = await addRes.Content.ReadFromJsonAsync<DriverResponseDto>();
        Assert.NotNull(created);
        Assert.Equal(driverEmail, created.Email);
        Assert.Equal("Employed Driver", created.FullName);

        // GetDrivers
        using var listReq = AuthedRequest(HttpMethod.Get, $"/api/v1/agencies/{agencyId}/drivers", agency.AccessToken);
        var listRes = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var drivers = await listRes.Content.ReadFromJsonAsync<List<DriverResponseDto>>();
        Assert.NotNull(drivers);
        Assert.Contains(drivers, d => d.Email == driverEmail);
    }

    [Fact]
    public async Task GetExpiringCompliance_Returns200_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/expiring-compliance?days=30", MintAdminToken());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetExpiringCompliance_Returns403_ForAgencyStaff()
    {
        var tokens = await RegisterAndLoginAgencyAsync("expiring-fail", "EXPFAIL");
        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/expiring-compliance", tokens.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
