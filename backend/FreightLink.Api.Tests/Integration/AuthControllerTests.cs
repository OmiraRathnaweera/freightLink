using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FreightLink.Api.DTOs.Auth;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-HTTP-pipeline tests for <c>AuthController</c>, covering behavior unit tests can't reach:
/// JWT middleware enforcement on protected routes, the real model-validation pipeline, and the
/// exact JSON shape returned over the wire.
/// </summary>
public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    /// <summary>Creates the test class with an HTTP client bound to the shared in-process test host.</summary>
    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>Registers a new shipper with a unique email and logs in, returning the credentials and issued tokens.</summary>
    private async Task<(string Email, string Password, TokenResponseDto Tokens)> RegisterAndLoginShipperAsync(string emailPrefix)
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

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });
        loginResponse.EnsureSuccessStatusCode();

        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>();
        return (email, password, tokens!);
    }

    /// <summary>
    /// Mints a validly-signed JWT using the same issuer/audience/key <see cref="CustomWebApplicationFactory"/>
    /// configures the host with, but with a caller-supplied claim set — used to simulate a token
    /// that passes signature/issuer/audience/lifetime validation but has an unexpected claim shape
    /// (e.g. missing NameIdentifier), which no token actually issued by this API's own
    /// TokenService.GenerateAccessToken can produce.
    /// </summary>
    private static string MintTokenWithClaims(IEnumerable<Claim> claims)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-that-is-long-enough-1234567890"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// GET /auth/me with a validly-signed token that has no NameIdentifier claim is rejected with a
    /// clean 401 and the standard error envelope, not an unhandled Guid.Parse exception surfacing
    /// as a generic 500 — no real token issued by this API can have this shape; this simulates a
    /// future JWT-validation/claim-shape change or a differently-configured issuer being accepted.
    /// </summary>
    [Fact]
    public async Task Me_Returns401WithErrorEnvelope_ForTokenMissingUserIdClaim()
    {
        var tokenWithoutUserId = MintTokenWithClaims(new[]
        {
            new Claim(JwtRegisteredClaimNames.Email, "no-user-id-claim@example.com")
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenWithoutUserId);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("UNAUTHORIZED", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>GET /auth/me with a valid Bearer token returns the caller's own profile.</summary>
    [Fact]
    public async Task Me_ReturnsCurrentUser_WithValidAccessToken()
    {
        var (email, _, tokens) = await RegisterAndLoginShipperAsync("me-valid");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponseDto>();
        Assert.Equal(email, user!.Email);
        Assert.Equal("Shipper", user.Role);
    }

    /// <summary>
    /// GET /auth/me without an Authorization header is rejected with 401 and the standard error
    /// envelope (JwtBearerEvents.OnChallenge), not ASP.NET's default bare/bodyless 401.
    /// </summary>
    [Fact]
    public async Task Me_Returns401WithErrorEnvelope_WithoutAuthorizationHeader()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("UNAUTHORIZED", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("error").GetProperty("message").GetString()));
    }

    /// <summary>
    /// GET /auth/me with a malformed/invalid token is rejected with 401 and the standard error
    /// envelope (JwtBearerEvents.OnChallenge), not ASP.NET's default bare/bodyless 401.
    /// </summary>
    [Fact]
    public async Task Me_Returns401WithErrorEnvelope_WithInvalidToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-jwt");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("UNAUTHORIZED", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("error").GetProperty("message").GetString()));
    }

    /// <summary>
    /// The 401 error envelope's response Content-Type is application/json, matching every other
    /// error response in this project — not the default WWW-Authenticate-header-only response.
    /// </summary>
    [Fact]
    public async Task Me_Returns401_WithJsonContentType_ForMissingToken()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>The /auth/me JSON response never includes a password-hash-like field.</summary>
    [Fact]
    public async Task Me_ResponseDoesNotExposePasswordHashOrSecrets()
    {
        var (_, _, tokens) = await RegisterAndLoginShipperAsync("me-safe");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);

        var propertyNames = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.DoesNotContain(propertyNames, name => name.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The issued access token's JWT claims include the user id (sub), email, and role.</summary>
    [Fact]
    public async Task AccessToken_ContainsUserIdEmailAndRoleClaims()
    {
        var (email, _, tokens) = await RegisterAndLoginShipperAsync("claims");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);

        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == email);
        Assert.Contains(jwt.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Shipper");
    }

    /// <summary>
    /// A password that exceeds BCrypt's 72-byte limit when UTF-8 encoded is rejected with 400, not
    /// silently truncated and hashed anyway. Uses multi-byte characters specifically to prove the
    /// check is byte-aware, not just a char-count check (73 emoji chars is well over 72 bytes).
    /// </summary>
    [Fact]
    public async Task RegisterShipper_Returns400WithValidationErrorEnvelope_ForPasswordExceedingUtf8ByteLimit()
    {
        var oversizedPassword = "Aa1!" + new string('中', 30); // 4 ASCII bytes + 30 * 3-byte CJK chars = 94 bytes, well under 72 chars

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = "toolongpass@example.com",
            Password = oversizedPassword,
            FullName = "Too Long Password",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("VALIDATION_ERROR", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>A weak password on shipper registration is rejected with 400 and the standard VALIDATION_ERROR envelope.</summary>
    [Fact]
    public async Task RegisterShipper_Returns400WithValidationErrorEnvelope_ForWeakPassword()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = "weakpass@example.com",
            Password = "weak",
            FullName = "Weak Password",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("VALIDATION_ERROR", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>
    /// A phone number missing the mandatory leading '+' is rejected with 400, not left to reach the
    /// DB's ck_user_phone_e164 CHECK and fail as an unhandled 500.
    /// </summary>
    [Fact]
    public async Task RegisterShipper_Returns400WithValidationErrorEnvelope_ForPhoneMissingPlusSign()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = "phonenoplus@example.com",
            Password = "Sup3r$ecret1",
            FullName = "No Plus Sign",
            PhoneE164 = "94771234567",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("VALIDATION_ERROR", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>
    /// An email with no TLD dot passes the loose [EmailAddress] check but must still be rejected
    /// with 400 by the regex that mirrors the DB's ck_user_email_format CHECK.
    /// </summary>
    [Fact]
    public async Task RegisterShipper_Returns400WithValidationErrorEnvelope_ForEmailMissingTld()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = "user@localhost",
            Password = "Sup3r$ecret1",
            FullName = "No Tld",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("VALIDATION_ERROR", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>A well-formed E.164 phone number (with leading '+') still registers successfully.</summary>
    [Fact]
    public async Task RegisterShipper_Succeeds_WithValidE164Phone()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = $"validphone-{Guid.NewGuid():N}@example.com",
            Password = "Sup3r$ecret1",
            FullName = "Valid Phone",
            PhoneE164 = "+14155552671",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>Logging in with the wrong password returns 401 with the standard INVALID_CREDENTIALS error envelope.</summary>
    [Fact]
    public async Task Login_Returns401WithErrorEnvelope_ForWrongPassword()
    {
        var (email, _, _) = await RegisterAndLoginShipperAsync("wrongpass");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = "DefinitelyWrong1!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("INVALID_CREDENTIALS", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>No public admin-registration route exists — hitting one returns 404.</summary>
    [Fact]
    public async Task NoPublicAdminRegistrationEndpointExists()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/admin", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
