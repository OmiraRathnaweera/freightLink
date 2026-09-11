using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Trips;
using FreightLink.Api.Entities.Enums;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-HTTP-pipeline tests for <c>TripsController</c>, covering only the role/authentication
/// contract — mirrors <c>LoadsControllerTests</c>'s scope-split reasoning, but more narrowly, since
/// <c>TripService</c> is currently a skeleton: every method throws <see cref="NotImplementedException"/>
/// unconditionally, so no ownership resolution, status-transition validation, or evidence-role
/// pairing logic exists yet to test. Endpoint-logic coverage for all of that belongs in a future
/// <c>TripServiceTests</c> once <c>TripService.cs</c> is genuinely implemented.
///
/// <para>
/// <b>TEMPORARY:</b> every "correctly-authorized" test below asserts <c>500 INTERNAL_SERVER_ERROR</c>
/// as its expected outcome — this is <i>only</i> correct while <c>TripService</c> is a stub. Once real
/// logic replaces a given method, the corresponding test(s) here must be rewritten to assert the real
/// expected result (e.g. 200/404/422) instead of deleted, so the role-gating coverage isn't lost.
/// </para>
/// </summary>
public class TripsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    /// <summary>Creates the test class with an HTTP client bound to the shared in-process test host.</summary>
    public TripsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>Registers a new shipper with a unique email and logs in, returning the issued tokens.</summary>
    private async Task<TokenResponseDto> RegisterAndLoginShipperAsync(string emailPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecret1";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = email,
            Password = password,
            FullName = "Integration Tester",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    /// <summary>
    /// Mints a validly-signed JWT carrying one <c>ClaimTypes.Role</c> claim per entry in
    /// <paramref name="roles"/>. Used here for AgencyStaff, Driver, and Admin — there is no public
    /// Driver registration endpoint at all, no public Admin registration endpoint, and (per
    /// <c>CustomWebApplicationFactory</c>) the test host's admin seed is disabled — so minting is the
    /// only way to obtain a token for any of these three roles in this test environment. Identical to
    /// <c>LoadsControllerTests.MintTokenWithRoles</c>.
    /// </summary>
    private static string MintTokenWithRoles(params string[] roles)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-that-is-long-enough-1234567890"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>A valid ChangeTripStatusDto payload — sent even on expected-403 tests, so a failing assertion unambiguously means the role gate, not a validation error, produced the result.</summary>
    private static ChangeTripStatusDto ValidChangeStatusDto() => new() { TargetStatus = TripStatus.PickedUp, Notes = "integration test" };

    /// <summary>A valid UploadTripEvidenceDto payload — same defensive reasoning as <see cref="ValidChangeStatusDto"/>.</summary>
    private static UploadTripEvidenceDto ValidUploadEvidenceDto() => new() { PublicId = "test-file-123", EvidenceType = EvidenceType.PickupProof };

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        return json.RootElement.GetProperty("error").GetProperty("code").GetString()!;
    }

    // --- Unauthenticated: every Trips endpoint requires a token ---

    /// <summary>GET /trips without a token is 401.</summary>
    [Fact]
    public async Task GetList_Returns401_WithoutToken()
    {
        var response = await _client.GetAsync("/api/v1/trips");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>GET /trips/{id} without a token is 401.</summary>
    [Fact]
    public async Task GetById_Returns401_WithoutToken()
    {
        var response = await _client.GetAsync($"/api/v1/trips/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>POST /trips/{id}/status without a token is 401.</summary>
    [Fact]
    public async Task ChangeStatus_Returns401_WithoutToken()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/trips/{Guid.NewGuid()}/status", ValidChangeStatusDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>POST /trips/{id}/evidence without a token is 401.</summary>
    [Fact]
    public async Task UploadEvidence_Returns401_WithoutToken()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/trips/{Guid.NewGuid()}/evidence", ValidUploadEvidenceDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>GET /trips/{id}/evidence without a token is 401.</summary>
    [Fact]
    public async Task GetEvidence_Returns401_WithoutToken()
    {
        var response = await _client.GetAsync($"/api/v1/trips/{Guid.NewGuid()}/evidence");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- GET /trips (AgencyStaff, Driver, Admin; Shipper excluded) ---

    /// <summary>A Shipper cannot call the trips list/dashboard endpoint — role gating.</summary>
    [Fact]
    public async Task GetList_Returns403_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-list-shipper");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/v1/trips", tokens.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An AgencyStaff caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetList_ReachesStub_ForAgencyStaff()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/v1/trips", MintTokenWithRoles("AgencyStaff")));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>A Driver caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetList_ReachesStub_ForDriver()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/v1/trips", MintTokenWithRoles("Driver")));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>An Admin caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetList_ReachesStub_ForAdmin()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/v1/trips", MintTokenWithRoles("Admin")));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    // --- GET /trips/{id} (all four roles admitted by the role gate) ---

    /// <summary>A Shipper caller reaches the (stub) service layer — unlike GetList, Shipper is admitted here per the API contract.</summary>
    [Fact]
    public async Task GetById_ReachesStub_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-getbyid-shipper");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}", tokens.AccessToken));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>An AgencyStaff caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetById_ReachesStub_ForAgencyStaff()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}", MintTokenWithRoles("AgencyStaff")));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>A Driver caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetById_ReachesStub_ForDriver()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}", MintTokenWithRoles("Driver")));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>An Admin caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetById_ReachesStub_ForAdmin()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}", MintTokenWithRoles("Admin")));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    // --- POST /trips/{id}/status (AgencyStaff, Driver only) ---

    /// <summary>A Shipper cannot advance a trip's status — role gating.</summary>
    [Fact]
    public async Task ChangeStatus_Returns403_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-status-shipper");

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/status", tokens.AccessToken);
        request.Content = JsonContent.Create(ValidChangeStatusDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An Admin cannot advance a trip's status — deliberately excluded from this write endpoint per the API contract, unlike the read endpoints above.</summary>
    [Fact]
    public async Task ChangeStatus_Returns403_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/status", MintTokenWithRoles("Admin"));
        request.Content = JsonContent.Create(ValidChangeStatusDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An AgencyStaff caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task ChangeStatus_ReachesStub_ForAgencyStaff()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/status", MintTokenWithRoles("AgencyStaff"));
        request.Content = JsonContent.Create(ValidChangeStatusDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>A Driver caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task ChangeStatus_ReachesStub_ForDriver()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/status", MintTokenWithRoles("Driver"));
        request.Content = JsonContent.Create(ValidChangeStatusDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    // --- POST /trips/{id}/evidence (AgencyStaff, Driver only) ---

    /// <summary>A Shipper cannot submit trip evidence — role gating.</summary>
    [Fact]
    public async Task UploadEvidence_Returns403_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-evidence-shipper");

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/evidence", tokens.AccessToken);
        request.Content = JsonContent.Create(ValidUploadEvidenceDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An Admin cannot submit trip evidence — deliberately excluded from this write endpoint.</summary>
    [Fact]
    public async Task UploadEvidence_Returns403_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("Admin"));
        request.Content = JsonContent.Create(ValidUploadEvidenceDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An AgencyStaff caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task UploadEvidence_ReachesStub_ForAgencyStaff()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("AgencyStaff"));
        request.Content = JsonContent.Create(ValidUploadEvidenceDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>A Driver caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task UploadEvidence_ReachesStub_ForDriver()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("Driver"));
        request.Content = JsonContent.Create(ValidUploadEvidenceDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    // --- GET /trips/{id}/evidence (all four roles admitted) ---

    /// <summary>A Shipper caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetEvidence_ReachesStub_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-getevidence-shipper");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}/evidence", tokens.AccessToken));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>An AgencyStaff caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetEvidence_ReachesStub_ForAgencyStaff()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("AgencyStaff")));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>A Driver caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetEvidence_ReachesStub_ForDriver()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("Driver")));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>An Admin caller reaches the (stub) service layer.</summary>
    [Fact]
    public async Task GetEvidence_ReachesStub_ForAdmin()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("Admin")));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_SERVER_ERROR", await ReadErrorCodeAsync(response));
    }
}