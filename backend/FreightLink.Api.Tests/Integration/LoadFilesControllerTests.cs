using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.DTOs.Loads;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-HTTP-pipeline tests for <c>LoadFilesController</c>, covering role/ownership authorization and
/// the attach/list/detach business rules that only exist at this layer (model binding of the
/// <c>FileType</c> enum, the standard error envelope). Uses <see cref="FakeFileStorageService"/> (via
/// <see cref="CustomWebApplicationFactory"/>) so no real Cloudinary call happens.
/// </summary>
public class LoadFilesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    /// <summary>Creates the test class with an HTTP client bound to the shared in-process test host.</summary>
    public LoadFilesControllerTests(CustomWebApplicationFactory factory)
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

    /// <summary>Mints a validly-signed Admin-role JWT, mirroring <c>LoadsControllerTests.MintAdminToken</c>.</summary>
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

    /// <summary>Uploads a fake file as the given caller via the shared <c>/api/v1/files/single</c> endpoint.</summary>
    private async Task<FileUploadResultDto> UploadFileAsShipperAsync(string accessToken)
    {
        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/files/single", accessToken);
        var content = new MultipartFormDataContent();
        // FileUploadValidator sniffs the leading bytes against the extension's magic number — a
        // ".pdf" upload must actually start with "%PDF" or it's rejected as BLOCKED_FILE_TYPE.
        var fileContent = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 fake pdf body"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "manifest.pdf");
        request.Content = content;

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileUploadResultDto>())!;
    }

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        return json.RootElement.GetProperty("error").GetProperty("code").GetString()!;
    }

    // --- Attach (Shipper own only) ---

    [Fact]
    public async Task Attach_OwnLoad_Returns201WithJoinedFileData()
    {
        var tokens = await RegisterAndLoginShipperAsync("attach-own");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(tokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken);
        request.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "Manifest" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoadFileResponseDto>();
        Assert.Equal(load.LoadId, result!.LoadId);
        Assert.Equal(uploaded.PublicId, result.PublicId);
        Assert.Equal(uploaded.SecureUrl, result.SecureUrl);
    }

    [Fact]
    public async Task Attach_AnotherShippersLoad_Returns403LoadNotOwned()
    {
        var ownerTokens = await RegisterAndLoginShipperAsync("attach-owner");
        var otherTokens = await RegisterAndLoginShipperAsync("attach-other");
        var load = await CreateLoadAsShipperAsync(ownerTokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(otherTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/files", otherTokens.AccessToken);
        request.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "Manifest" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("LOAD_NOT_OWNED", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Attach_NonexistentLoad_Returns404LoadNotFound()
    {
        var tokens = await RegisterAndLoginShipperAsync("attach-missing-load");
        var uploaded = await UploadFileAsShipperAsync(tokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{Guid.NewGuid()}/files", tokens.AccessToken);
        request.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "Manifest" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("LOAD_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Attach_UploadedFileNotOwnedByCaller_Returns403FileNotOwned()
    {
        var tokens = await RegisterAndLoginShipperAsync("attach-not-owned");
        var otherTokens = await RegisterAndLoginShipperAsync("attach-not-owned-uploader");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(otherTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken);
        request.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "Manifest" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FILE_NOT_OWNED", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Attach_UploadedFileAlreadyAttachedElsewhere_Returns409FileInUse()
    {
        var tokens = await RegisterAndLoginShipperAsync("attach-in-use");
        var loadA = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var loadB = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(tokens.AccessToken);

        using (var firstAttach = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadA.LoadId}/files", tokens.AccessToken))
        {
            firstAttach.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "Manifest" });
            (await _client.SendAsync(firstAttach)).EnsureSuccessStatusCode();
        }

        using var secondAttach = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadB.LoadId}/files", tokens.AccessToken);
        secondAttach.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "Manifest" });
        var response = await _client.SendAsync(secondAttach);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("FILE_IN_USE", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Attach_InvalidFileTypeString_Returns400ValidationError()
    {
        var tokens = await RegisterAndLoginShipperAsync("attach-invalid-type");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(tokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken);
        request.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "NotARealType" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Attach_MissingFileType_Returns400ValidationError()
    {
        var tokens = await RegisterAndLoginShipperAsync("attach-missing-type");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(tokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken);
        request.Content = JsonContent.Create(new { publicId = uploaded.PublicId });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>
    /// A raw numeric fileType (e.g. the JSON number 99, with no corresponding FileType member) must
    /// be rejected the same as an invalid string — proves FileTypeJsonConverter's
    /// allowIntegerValues:false actually takes effect over the wire, rather than the plain
    /// JsonStringEnumConverter's default of silently binding any underlying integer, defined or not.
    /// </summary>
    [Fact]
    public async Task Attach_NumericFileType_Returns400ValidationError()
    {
        var tokens = await RegisterAndLoginShipperAsync("attach-numeric-type");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(tokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken);
        request.Content = new StringContent(
            $"{{\"publicId\":\"{uploaded.PublicId}\",\"fileType\":99}}",
            Encoding.UTF8,
            "application/json");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Attach_Unauthenticated_Returns401()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/loads/{Guid.NewGuid()}/files", new { publicId = "x", fileType = "Manifest" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- List (Shipper own, Admin any) ---

    [Fact]
    public async Task List_OwnLoad_ReturnsAttachedFiles()
    {
        var tokens = await RegisterAndLoginShipperAsync("list-own");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(tokens.AccessToken);
        using (var attach = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken))
        {
            attach.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "Manifest" });
            (await _client.SendAsync(attach)).EnsureSuccessStatusCode();
        }

        using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var files = await response.Content.ReadFromJsonAsync<List<LoadFileResponseDto>>();
        Assert.Single(files!);
    }

    [Fact]
    public async Task List_AsAdmin_ReturnsAttachedFilesForAnyLoad()
    {
        var tokens = await RegisterAndLoginShipperAsync("list-admin");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(tokens.AccessToken);
        using (var attach = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken))
        {
            attach.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "Manifest" });
            (await _client.SendAsync(attach)).EnsureSuccessStatusCode();
        }

        using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{load.LoadId}/files", MintAdminToken());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var files = await response.Content.ReadFromJsonAsync<List<LoadFileResponseDto>>();
        Assert.Single(files!);
    }

    [Fact]
    public async Task List_NotOwnedByNonAdmin_Returns403LoadNotOwned()
    {
        var ownerTokens = await RegisterAndLoginShipperAsync("list-owner");
        var otherTokens = await RegisterAndLoginShipperAsync("list-other");
        var load = await CreateLoadAsShipperAsync(ownerTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{load.LoadId}/files", otherTokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("LOAD_NOT_OWNED", await ReadErrorCodeAsync(response));
    }

    // --- Detach (Shipper own only) ---

    [Fact]
    public async Task Detach_OwnLoad_Returns204AndRemovesAttachment()
    {
        var tokens = await RegisterAndLoginShipperAsync("detach-own");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);
        var uploaded = await UploadFileAsShipperAsync(tokens.AccessToken);
        LoadFileResponseDto attached;
        using (var attach = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken))
        {
            attach.Content = JsonContent.Create(new { publicId = uploaded.PublicId, fileType = "Manifest" });
            var attachResponse = await _client.SendAsync(attach);
            attachResponse.EnsureSuccessStatusCode();
            attached = (await attachResponse.Content.ReadFromJsonAsync<LoadFileResponseDto>())!;
        }

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/loads/{load.LoadId}/files/{attached.FileId}", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var listRequest = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{load.LoadId}/files", tokens.AccessToken);
        var listResponse = await _client.SendAsync(listRequest);
        var files = await listResponse.Content.ReadFromJsonAsync<List<LoadFileResponseDto>>();
        Assert.Empty(files!);
    }

    [Fact]
    public async Task Detach_AnotherShippersLoad_Returns403LoadNotOwned()
    {
        var ownerTokens = await RegisterAndLoginShipperAsync("detach-owner");
        var otherTokens = await RegisterAndLoginShipperAsync("detach-other");
        var load = await CreateLoadAsShipperAsync(ownerTokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/loads/{load.LoadId}/files/{Guid.NewGuid()}", otherTokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("LOAD_NOT_OWNED", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Detach_NonexistentLoadFile_Returns404LoadFileNotFound()
    {
        var tokens = await RegisterAndLoginShipperAsync("detach-missing");
        var load = await CreateLoadAsShipperAsync(tokens.AccessToken);

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/loads/{load.LoadId}/files/{Guid.NewGuid()}", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("LOAD_FILE_NOT_FOUND", await ReadErrorCodeAsync(response));
    }
}
