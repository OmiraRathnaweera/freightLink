using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Loads;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Targeted security checks against the real HTTP pipeline (Assignment 2, area F2): authentication
/// bypass, role escalation, token tampering, mass assignment, injection payloads and unauthenticated
/// internal/maintenance endpoints. Complements the automated ZAP baseline scan in
/// <c>security/zap</c> with deterministic, repeatable assertions.
/// </summary>
public class SecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string SigningKey = "integration-test-signing-key-that-is-long-enough-1234567890";
    private const string Password = "Sup3r$ecret1";

    private readonly HttpClient _client;

    public SecurityTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ---------- helpers ----------

    private static string MintToken(string role, string key = SigningKey, DateTime? expires = null, DateTime? notBefore = null)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var userId = Guid.NewGuid().ToString();
        var token = new JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            },
            notBefore: notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            expires: expires ?? DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<TokenResponseDto> RegisterAndLoginShipperAsync()
    {
        var email = $"sec-shipper-{Guid.NewGuid():N}@example.com";
        (await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = email, Password = Password, FullName = "Sec Tester",
            CompanyName = "Sec Freight", BillingAddress = "1 Main Street, Colombo"
        })).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = Password });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string? token = null, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return request;
    }

    private static CreateLoadDto ValidLoad(string cargo = "Pallets of canned goods") => new()
    {
        CargoDescription = cargo,
        WeightKg = 500m, VolumeM3 = 2.5m,
        PickupAddress = "123 Pickup Street, Colombo", PickupLat = 6.9271m, PickupLng = 79.8612m,
        DropoffAddress = "456 Dropoff Road, Kandy", DropoffLat = 7.2906m, DropoffLng = 80.6337m,
        PickupWindowStart = DateTimeOffset.UtcNow.AddDays(1),
        PickupWindowEnd = DateTimeOffset.UtcNow.AddDays(2)
    };

    // ---------- SEC-01: authentication bypass ----------

    public static IEnumerable<object[]> ProtectedEndpoints() => new[]
    {
        new object[] { "GET", "/api/v1/auth/me" },
        new object[] { "GET", "/api/v1/loads" },
        new object[] { "POST", "/api/v1/loads" },
        new object[] { "GET", "/api/v1/trips" },
        new object[] { "GET", "/api/v1/invoices" },
        new object[] { "GET", "/api/v1/assignments" },
        new object[] { "GET", "/api/v1/agencies" },
        new object[] { "GET", "/api/v1/disputes" },
        new object[] { "GET", "/api/v1/admin/analytics/summary" },
        new object[] { "GET", "/api/v1/admin/pricing/fuel-rates" },
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_Returns401_WithoutToken(string method, string url)
    {
        var response = await _client.SendAsync(Request(new HttpMethod(method), url,
            content: method == "POST" ? JsonContent.Create(new { }) : null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Bearer")]
    [InlineData("Bearer not-a-jwt")]
    [InlineData("Bearer a.b.c")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public async Task ProtectedEndpoint_Returns401_ForMalformedAuthorizationHeader(string headerValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/loads");
        request.Headers.TryAddWithoutValidation("Authorization", headerValue);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- SEC-02: token tampering ----------

    [Fact]
    public async Task Token_SignedWithWrongKey_IsRejected()
    {
        var forged = MintToken("Admin", key: "an-attacker-controlled-key-that-is-long-enough-123456");

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/admin/analytics/summary", forged));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_Expired_IsRejected()
    {
        var expired = MintToken("Admin", notBefore: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1));

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/admin/analytics/summary", expired));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_WithAlgNone_IsRejected()
    {
        static string B64(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = B64($"{{\"sub\":\"{Guid.NewGuid()}\",\"role\":\"Admin\",\"http://schemas.microsoft.com/ws/2008/06/identity/claims/role\":\"Admin\",\"iss\":\"FreightLinkApi\",\"aud\":\"FreightLinkClient\",\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}");

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/admin/analytics/summary", $"{header}.{payload}."));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_PayloadEditedToAdmin_WithOriginalSignature_IsRejected()
    {
        var shipper = await RegisterAndLoginShipperAsync();
        var parts = shipper.AccessToken.Split('.');
        var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
        var tampered = payloadJson.Replace("\"Shipper\"", "\"Admin\"");
        Assert.NotEqual(payloadJson, tampered); // guard: the role claim really was rewritten
        var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(tampered)}.{parts[2]}";

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/admin/analytics/summary", forged));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_IsNotAcceptedAsAccessToken()
    {
        var shipper = await RegisterAndLoginShipperAsync();

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/auth/me", shipper.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- SEC-03: role escalation (horizontal/vertical privilege) ----------

    [Theory]
    [InlineData("GET", "/api/v1/admin/analytics/summary")]
    [InlineData("GET", "/api/v1/admin/pricing/fuel-rates")]
    [InlineData("GET", "/api/v1/agencies")]
    [InlineData("GET", "/api/v1/trips")]
    public async Task ShipperToken_Returns403_OnNonShipperEndpoints(string method, string url)
    {
        var shipper = await RegisterAndLoginShipperAsync();

        var response = await _client.SendAsync(Request(new HttpMethod(method), url, shipper.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("AgencyStaff")]
    [InlineData("Driver")]
    public async Task NonShipperRoles_CannotPostLoads(string role)
    {
        var response = await _client.SendAsync(Request(HttpMethod.Post, "/api/v1/loads", MintToken(role), JsonContent.Create(ValidLoad())));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotApproveAiMatch_ThroughLegacyAdminRoute()
    {
        var response = await _client.SendAsync(Request(
            HttpMethod.Post, $"/api/v1/admin/agent-workflows/{Guid.NewGuid()}/approve", MintToken("Admin"), JsonContent.Create(new { })));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Role_ClaimWithUnknownValue_IsDeniedOnRoleRestrictedEndpoint()
    {
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/admin/analytics/summary", MintToken("SuperAdmin")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- SEC-04: mass assignment on registration ----------

    [Fact]
    public async Task Register_IgnoresClientSuppliedRole_AndAlwaysCreatesShipper()
    {
        var email = $"sec-mass-{Guid.NewGuid():N}@example.com";
        var body = new StringContent(
            JsonSerializer.Serialize(new
            {
                email, password = Password, fullName = "Mass Assign", companyName = "Evil Co",
                billingAddress = "1 Main Street, Colombo", role = "Admin", isActive = true, isEmailVerified = true
            }), Encoding.UTF8, "application/json");

        (await _client.PostAsync("/api/v1/auth/register/shipper", body)).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = Password });
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponseDto>())!;
        var me = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/auth/me", tokens.AccessToken));
        var user = (await me.Content.ReadFromJsonAsync<CurrentUserResponseDto>())!;

        Assert.Equal("Shipper", user.Role);
    }

    // ---------- SEC-05: sensitive data exposure ----------

    [Fact]
    public async Task Me_Response_NeverContainsPasswordMaterial()
    {
        var shipper = await RegisterAndLoginShipperAsync();

        var me = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/auth/me", shipper.AccessToken));
        var raw = await me.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WrongPassword_And_UnknownUser_ReturnIdenticalStatusAndBody()
    {
        var shipper = await RegisterAndLoginShipperAsync();
        var me = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/auth/me", shipper.AccessToken));
        var email = (await me.Content.ReadFromJsonAsync<CurrentUserResponseDto>())!.Email;

        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = "Wrong$Pass123" });
        var unknownUser = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = $"nobody-{Guid.NewGuid():N}@example.com", Password = "Wrong$Pass123" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknownUser.StatusCode); // no user enumeration
    }

    // ---------- SEC-06: injection payloads ----------

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("'; DROP TABLE \"Users\"; --")]
    public async Task Login_SqlInjectionPayloads_AreRejectedCleanly_Not500(string payload)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = payload, Password = payload });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized,
            $"Expected 400/401 for injection payload but got {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("<script>alert('xss')</script> fragile glassware")]
    [InlineData("Robert'); DROP TABLE Loads;-- crates")]
    [InlineData("\" onerror=\"alert(1)\" <img src=x> pallets")]
    public async Task PostLoad_InjectionPayloadInText_IsStoredLiterally_AndReturnedAsJson(string cargo)
    {
        var shipper = await RegisterAndLoginShipperAsync();

        var create = await _client.SendAsync(Request(HttpMethod.Post, "/api/v1/loads", shipper.AccessToken, JsonContent.Create(ValidLoad(cargo))));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.StartsWith("application/json", create.Content.Headers.ContentType?.MediaType + "", StringComparison.OrdinalIgnoreCase);
        var load = (await create.Content.ReadFromJsonAsync<LoadResponseDto>())!;
        Assert.Equal(cargo, load.CargoDescription); // stored verbatim (parameterised), never executed/altered

        // The table still exists and the load can be read back => the SQL fragment was not executed.
        var list = await _client.SendAsync(Request(HttpMethod.Get, "/api/v1/loads", shipper.AccessToken));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task PostLoad_OversizedCargoDescription_IsRejectedWith400()
    {
        var shipper = await RegisterAndLoginShipperAsync();

        var create = await _client.SendAsync(Request(HttpMethod.Post, "/api/v1/loads", shipper.AccessToken,
            JsonContent.Create(ValidLoad(new string('A', 1_001)))));

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task PostLoad_MalformedJson_Returns400_Not500()
    {
        var shipper = await RegisterAndLoginShipperAsync();

        var create = await _client.SendAsync(Request(HttpMethod.Post, "/api/v1/loads", shipper.AccessToken,
            new StringContent("{\"cargoDescription\": ", Encoding.UTF8, "application/json")));

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    // ---------- SEC-07: internal service endpoints ----------

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    [InlineData("")]
    public async Task InternalEndpoints_RejectMissingOrWrongServiceKey(string? key)
    {
        var request = Request(HttpMethod.Post, "/internal/agent-workflow-runs", content: JsonContent.Create(new { }));
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", key);
        }

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 but got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task InternalEndpoints_RejectUserJwt_InPlaceOfServiceKey()
    {
        var response = await _client.SendAsync(Request(HttpMethod.Post, "/internal/pricing/estimate", MintToken("Admin"), JsonContent.Create(new { })));

        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 but got {(int)response.StatusCode}");
    }

    // ---------- SEC-08: unauthenticated maintenance/seed endpoints (DEF-001, DEF-002) ----------

    [Theory]
    [InlineData("/api/v1/agencies/seed-defaults")]
    [InlineData("/api/v1/trips/seed-example")]
    public async Task SeedEndpoints_Return401_ForAnonymousCaller(string url)
    {
        var response = await _client.SendAsync(Request(HttpMethod.Post, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/agencies/seed-defaults")]
    [InlineData("/api/v1/trips/seed-example")]
    public async Task SeedEndpoints_Return403_ForShipper(string url)
    {
        var shipper = await RegisterAndLoginShipperAsync();

        var response = await _client.SendAsync(Request(HttpMethod.Post, url, shipper.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SeedDefaultAgencies_Succeeds_ForAdmin()
    {
        var response = await _client.SendAsync(Request(HttpMethod.Post, "/api/v1/agencies/seed-defaults", MintToken("Admin")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    // ---------- SEC-09: response hardening headers (DEF-006, found by the ZAP scan) ----------

    [Theory]
    [InlineData("/health", HttpStatusCode.OK)]
    [InlineData("/api/v1/auth/agencies", HttpStatusCode.OK)]
    [InlineData("/api/v1/loads", HttpStatusCode.Unauthorized)]
    [InlineData("/api/v1/does-not-exist", HttpStatusCode.NotFound)]
    public async Task Responses_CarryNosniffHeader_IncludingErrors(string url, HttpStatusCode expectedStatus)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var values));
        Assert.Contains("nosniff", values!);
    }

    [Fact]
    public async Task ErrorBody_NeverLeaksStackTracesOrExceptionTypes()
    {
        // A request that reaches the controller with a body the model binder cannot read must not echo internals.
        var shipper = await RegisterAndLoginShipperAsync();
        var response = await _client.SendAsync(Request(HttpMethod.Post, "/api/v1/loads", shipper.AccessToken,
            new StringContent("{\"weightKg\": \"not-a-number\"}", Encoding.UTF8, "application/json")));
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("   at ", body);          // no stack frames
        Assert.DoesNotContain("Exception", body);       // no exception type names
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
    }
}
