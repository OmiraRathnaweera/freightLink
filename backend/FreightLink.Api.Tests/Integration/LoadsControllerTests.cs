using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Loads;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-HTTP-pipeline tests for <c>LoadsController</c>, focused on the role/ownership authorization
/// behavior that only exists at this layer: JWT role gating (<c>[Authorize(Roles = ...)]</c>) and the
/// Shipper-ownership/Admin-bypass rules <see cref="Services.LoadService"/> enforces. Endpoint-logic
/// coverage (pagination, transition rules, validation) lives in <c>LoadServiceTests</c>.
/// </summary>
public class LoadsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    /// <summary>Creates the test class with an HTTP client bound to the shared in-process test host.</summary>
    public LoadsControllerTests(CustomWebApplicationFactory factory)
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
    /// Mints a validly-signed Admin-role JWT using the same issuer/audience/key
    /// <see cref="CustomWebApplicationFactory"/> configures the host with. There is no public admin
    /// registration endpoint and the test host's admin seed is disabled (see
    /// <see cref="CustomWebApplicationFactory"/>), so this is the only way to obtain an Admin-role
    /// token in this test environment — mirrors <c>AuthControllerTests.MintTokenWithClaims</c>.
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

    /// <summary>
    /// Mints a validly-signed JWT carrying one <c>ClaimTypes.Role</c> claim per entry in
    /// <paramref name="roles"/>, in that order — lets a test give the token multiple role claims,
    /// which <c>[Authorize(Roles = ...)]</c>'s <c>User.IsInRole</c> matches against ANY of them,
    /// while <c>LoadsController.GetCurrentUserRole</c>'s <c>FindFirstValue</c> only ever reads the
    /// first.
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

    /// <summary>A valid Create payload.</summary>
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

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task<LoadResponseDto> CreateLoadAsShipperAsync(string accessToken)
    {
        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/loads", accessToken);
        request.Content = JsonContent.Create(ValidCreateLoadDto());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoadResponseDto>())!;
    }

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        return json.RootElement.GetProperty("error").GetProperty("code").GetString()!;
    }

    // --- Unauthenticated ---

    /// <summary>Every Load endpoint requires authentication; POST /loads without a token is 401.</summary>
    [Fact]
    public async Task Create_Returns401_WithoutToken()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/loads", ValidCreateLoadDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>GET /loads/{id} without a token is 401.</summary>
    [Fact]
    public async Task GetById_Returns401_WithoutToken()
    {
        var response = await _client.GetAsync($"/api/v1/loads/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Create (Shipper only) ---

    /// <summary>A Shipper can create a load; the response is 201 with the created resource.</summary>
    [Fact]
    public async Task Create_Returns201_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("create");

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/loads", tokens.AccessToken);
        request.Content = JsonContent.Create(ValidCreateLoadDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<LoadResponseDto>();
        Assert.Equal("Draft", created!.Status);
    }

    /// <summary>An Admin cannot create a load — Create is Shipper-only.</summary>
    [Fact]
    public async Task Create_Returns403_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/loads", MintAdminToken());
        request.Content = JsonContent.Create(ValidCreateLoadDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Omitting a coordinate field entirely (not sending <c>0</c>, actually leaving it out of the
    /// JSON body) is rejected as a 400 VALIDATION_ERROR rather than silently binding to <c>0</c> and
    /// creating a load at Null Island. Proves the DTO's coordinate properties are nullable so
    /// <c>[Required]</c> has something to actually check.
    /// </summary>
    [Theory]
    [InlineData("pickupLat")]
    [InlineData("pickupLng")]
    [InlineData("dropoffLat")]
    [InlineData("dropoffLng")]
    public async Task Create_Returns400_WhenACoordinateFieldIsOmitted(string fieldToOmit)
    {
        var tokens = await RegisterAndLoginShipperAsync("missing-coord");
        // Must serialize with the same camelCase policy the API's JSON pipeline uses — the default
        // JsonSerializer.SerializeToNode() overload produces PascalCase property names, so
        // fieldToOmit (e.g. "pickupLat") would never match a key and this test would create a
        // perfectly valid load instead of omitting anything.
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var body = JsonSerializer.SerializeToNode(ValidCreateLoadDto(), jsonOptions)!.AsObject();
        body.Remove(fieldToOmit);

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/loads", tokens.AccessToken);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCodeAsync(response));
    }

    // --- Get one (Shipper own, Admin any) ---

    /// <summary>A Shipper can fetch a load they own.</summary>
    [Fact]
    public async Task GetById_Returns200_ForOwningShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("get-own");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{load.LoadId}", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoadResponseDto>();
        Assert.Equal("Integration Tester", body!.ShipperName);
        var historyRow = Assert.Single(body.StatusHistory);
        Assert.Equal("Draft", historyRow.ToStatus);
        Assert.Null(historyRow.FromStatus);
    }

    /// <summary>A Shipper cannot fetch another Shipper's load — 403, not 404, per the contract's ownership-vs-not-found distinction.</summary>
    [Fact]
    public async Task GetById_Returns403_ForNonOwningShipper()
    {
        var ownerTokens = await RegisterAndLoginShipperAsync("get-owner");
        var otherTokens = await RegisterAndLoginShipperAsync("get-other");
        var load = await CreateLoadAsShipperAsync(ownerTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{load.LoadId}", otherTokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("LOAD_NOT_OWNED", await ReadErrorCodeAsync(response));
    }

    /// <summary>An Admin can fetch any load, regardless of who owns it.</summary>
    [Fact]
    public async Task GetById_Returns200_ForAdmin_OnAnyShippersLoad()
    {
        var ownerTokens = await RegisterAndLoginShipperAsync("get-admin-owner");
        var load = await CreateLoadAsShipperAsync(ownerTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{load.LoadId}", MintAdminToken());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Fetching a nonexistent load id is 404.</summary>
    [Fact]
    public async Task GetById_Returns404_ForNonexistentLoad()
    {
        var tokens = await RegisterAndLoginShipperAsync("get-missing");

        using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{Guid.NewGuid()}", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Get list (Shipper own, Admin all) ---

    /// <summary>A Shipper's list only ever contains their own loads.</summary>
    [Fact]
    public async Task GetList_ScopesToOwnLoads_ForShipper()
    {
        var shipperATokens = await RegisterAndLoginShipperAsync("list-a");
        var shipperBTokens = await RegisterAndLoginShipperAsync("list-b");
        await CreateLoadAsShipperAsync(shipperATokens.AccessToken);
        await CreateLoadAsShipperAsync(shipperBTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/loads", shipperATokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedLoadResponseDto>();
        Assert.Equal(1, page!.TotalItems);
    }

    /// <summary>An Admin's list includes loads from every shipper.</summary>
    [Fact]
    public async Task GetList_ReturnsLoadsAcrossShippers_ForAdmin()
    {
        var shipperATokens = await RegisterAndLoginShipperAsync("list-admin-a");
        var shipperBTokens = await RegisterAndLoginShipperAsync("list-admin-b");
        await CreateLoadAsShipperAsync(shipperATokens.AccessToken);
        await CreateLoadAsShipperAsync(shipperBTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/loads?pageSize=100", MintAdminToken());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedLoadResponseDto>();
        Assert.True(page!.TotalItems >= 2);
        Assert.All(page.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.ShipperName)));
    }

    /// <summary>
    /// A role claim with no corresponding <c>UserRole</c> member is rejected, even when a second,
    /// valid role claim on the same token ("Admin") is enough to satisfy the coarser
    /// <c>[Authorize(Roles = ...)]</c> pipeline check and let the request reach the action —
    /// <c>Enum.TryParse</c> alone would accept a bare numeric string like <c>"99"</c> as a
    /// technically-parseable but undefined <c>UserRole</c>, so this proves
    /// <c>LoadsController.GetCurrentUserRole</c>'s <c>Enum.IsDefined</c> check is what actually
    /// stops it from reaching <c>LoadService</c>.
    /// </summary>
    [Fact]
    public async Task GetList_Returns401_WhenFirstRoleClaimHasNoDefinedEnumMember()
    {
        var token = MintTokenWithRoles("99", "Admin");

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/loads", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>createdFrom/createdTo query params filter the list to the requested inclusive date range.</summary>
    [Fact]
    public async Task GetList_WithCreatedFromAndCreatedTo_FiltersByDateRange()
    {
        var tokens = await RegisterAndLoginShipperAsync("list-daterange");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));

        using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/loads?createdFrom={from}&createdTo={to}", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedLoadResponseDto>();
        Assert.Contains(page!.Items, item => item.LoadId == load.LoadId);
    }

    /// <summary>A createdFrom in the future excludes every load, returning an empty page rather than an error.</summary>
    [Fact]
    public async Task GetList_WithCreatedFromExcludingAllLoads_ReturnsEmptyItems()
    {
        var tokens = await RegisterAndLoginShipperAsync("list-daterange-empty");
        await CreateLoadAsShipperAsync(tokens.AccessToken);
        var futureFrom = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddYears(1).ToString("O"));

        using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/loads?createdFrom={futureFrom}", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedLoadResponseDto>();
        Assert.Empty(page!.Items);
    }

    // --- Edit (Shipper own only) ---

    /// <summary>A Shipper can edit a Draft load they own.</summary>
    [Fact]
    public async Task Update_Returns200_ForOwningShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("update-own");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/loads/{load.LoadId}", tokens.AccessToken);
        request.Content = JsonContent.Create(new UpdateLoadDto
        {
            CargoDescription = "Updated cargo description",
            WeightKg = 750m,
            VolumeM3 = 3m,
            PickupAddress = load.PickupAddress,
            PickupLat = load.PickupLat,
            PickupLng = load.PickupLng,
            DropoffAddress = load.DropoffAddress,
            DropoffLat = load.DropoffLat,
            DropoffLng = load.DropoffLng,
            PickupWindowStart = load.PickupWindowStart,
            PickupWindowEnd = load.PickupWindowEnd
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<LoadResponseDto>();
        Assert.Equal("Integration Tester", updated!.ShipperName);
    }

    /// <summary>A Shipper cannot edit another Shipper's load — 403.</summary>
    [Fact]
    public async Task Update_Returns403_ForNonOwningShipper()
    {
        var ownerTokens = await RegisterAndLoginShipperAsync("update-owner");
        var otherTokens = await RegisterAndLoginShipperAsync("update-other");
        var load = await CreateLoadAsShipperAsync(ownerTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/loads/{load.LoadId}", otherTokens.AccessToken);
        request.Content = JsonContent.Create(new UpdateLoadDto
        {
            CargoDescription = "Hijacked update",
            WeightKg = 750m,
            VolumeM3 = 3m,
            PickupAddress = load.PickupAddress,
            PickupLat = load.PickupLat,
            PickupLng = load.PickupLng,
            DropoffAddress = load.DropoffAddress,
            DropoffLat = load.DropoffLat,
            DropoffLng = load.DropoffLng,
            PickupWindowStart = load.PickupWindowStart,
            PickupWindowEnd = load.PickupWindowEnd
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("LOAD_NOT_OWNED", await ReadErrorCodeAsync(response));
    }

    /// <summary>An Admin is blocked from editing a Shipper's load — role gating, never reaches the service.</summary>
    [Fact]
    public async Task Update_Returns403_ForAdmin()
    {
        var ownerTokens = await RegisterAndLoginShipperAsync("update-admin-owner");
        var load = await CreateLoadAsShipperAsync(ownerTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/loads/{load.LoadId}", MintAdminToken());
        request.Content = JsonContent.Create(new UpdateLoadDto
        {
            CargoDescription = "Admin attempted update",
            WeightKg = 750m,
            VolumeM3 = 3m,
            PickupAddress = load.PickupAddress,
            PickupLat = load.PickupLat,
            PickupLng = load.PickupLng,
            DropoffAddress = load.DropoffAddress,
            DropoffLat = load.DropoffLat,
            DropoffLng = load.DropoffLng,
            PickupWindowStart = load.PickupWindowStart,
            PickupWindowEnd = load.PickupWindowEnd
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- Cancel (Shipper own only) ---

    /// <summary>A Shipper can cancel a load they own.</summary>
    [Fact]
    public async Task Cancel_Returns200_ForOwningShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("cancel-own");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Patch, $"/api/v1/loads/{load.LoadId}/cancel", tokens.AccessToken);
        request.Content = JsonContent.Create(new CancelLoadDto { Reason = "Shipper changed plans" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.Content.ReadFromJsonAsync<LoadResponseDto>();
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal("Integration Tester", cancelled.ShipperName);
    }

    /// <summary>An Admin is blocked from cancelling a Shipper's load — role gating, never reaches the service.</summary>
    [Fact]
    public async Task Cancel_Returns403_ForAdmin()
    {
        var ownerTokens = await RegisterAndLoginShipperAsync("cancel-admin-owner");
        var load = await CreateLoadAsShipperAsync(ownerTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Patch, $"/api/v1/loads/{load.LoadId}/cancel", MintAdminToken());
        request.Content = JsonContent.Create(new CancelLoadDto { Reason = "Admin attempted cancel" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Cancelling a load that's already Cancelled is rejected as 422 (invalid state transition),
    /// not 409 — confirms the contract's documented split (422 for state-transition rules, 409
    /// reserved for double-submit/concurrency races) rather than the generic "status conflict" label.
    /// </summary>
    [Fact]
    public async Task Cancel_Returns422_WhenLoadAlreadyCancelled()
    {
        var tokens = await RegisterAndLoginShipperAsync("cancel-twice");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        using (var firstCancel = AuthedRequest(HttpMethod.Patch, $"/api/v1/loads/{load.LoadId}/cancel", tokens.AccessToken))
        {
            firstCancel.Content = JsonContent.Create(new CancelLoadDto { Reason = "First cancellation" });
            (await _client.SendAsync(firstCancel)).EnsureSuccessStatusCode();
        }

        using var secondCancel = AuthedRequest(HttpMethod.Patch, $"/api/v1/loads/{load.LoadId}/cancel", tokens.AccessToken);
        secondCancel.Content = JsonContent.Create(new CancelLoadDto { Reason = "Second cancellation" });
        var response = await _client.SendAsync(secondCancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("INVALID_LOAD_STATUS_TRANSITION", await ReadErrorCodeAsync(response));
    }
}
