using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-HTTP-pipeline tests for <c>FilesController</c>, covering behavior unit tests can't reach:
/// JWT auth enforcement, multipart/form-data model binding, and the exact JSON shape returned over
/// the wire. Uses <see cref="FakeFileStorageService"/> (registered by
/// <see cref="CustomWebApplicationFactory"/>) so no real Cloudinary call happens.
/// </summary>
public class FilesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    /// <summary>Creates the test class with an HTTP client bound to the shared in-process test host.</summary>
    public FilesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Creates an Admin user directly in the test host's InMemory <see cref="AppDbContext"/> and
    /// mints a token for it via <see cref="ITokenService"/> — this test host clears the admin-seed
    /// env vars (see <see cref="CustomWebApplicationFactory"/>), so there is no seeded Admin to log
    /// in as, and Admin has no public registration endpoint to register one through.
    /// </summary>
    private async Task<string> GetAdminAccessTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var admin = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Admin,
            Email = $"admin-{Guid.NewGuid():N}@example.com",
            PasswordHash = "not-used-by-this-test",
            FullName = "Integration Admin",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();

        return tokenService.GenerateAccessToken(admin);
    }

    /// <summary>Registers a new shipper with a unique email and logs in, returning the issued access token.</summary>
    private async Task<string> GetAccessTokenAsync()
    {
        var email = $"files-{Guid.NewGuid():N}@example.com";
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

        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>();
        return tokens!.AccessToken;
    }

    private static MultipartFormDataContent SingleFileContent(string fileName, byte[]? bytes = null)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes ?? Encoding.UTF8.GetBytes("test content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    // --- Unauthenticated ---

    /// <summary>POST /files/single without a token is rejected with 401.</summary>
    [Fact]
    public async Task UploadSingle_Returns401_WithoutToken()
    {
        var response = await _client.PostAsync("/api/v1/files/single", SingleFileContent("photo.jpg"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Admin is not one of this feature's allowed roles (Shipper/AgencyStaff/Driver only) — rejected with 403.</summary>
    [Fact]
    public async Task UploadSingle_Returns403_ForAdminRole()
    {
        var token = await GetAdminAccessTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/single");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = SingleFileContent("cargo-photo.jpg");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- Upload single ---

    /// <summary>A valid authenticated single upload returns 201 with the stored file's details.</summary>
    [Fact]
    public async Task UploadSingle_Returns201_ForValidAuthenticatedUpload()
    {
        var token = await GetAccessTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/single");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = SingleFileContent("cargo-photo.jpg");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FileUploadResultDto>();
        Assert.False(string.IsNullOrWhiteSpace(result!.PublicId));
        Assert.False(string.IsNullOrWhiteSpace(result.SecureUrl));
    }

    /// <summary>
    /// Regression test for the bug this fix addresses, exercised through the real HTTP pipeline:
    /// a successful upload must leave a matching row in the <c>UploadedFiles</c> table, not just a
    /// Cloudinary asset.
    /// </summary>
    [Fact]
    public async Task UploadSingle_PersistsMetadataToDatabase()
    {
        var token = await GetAccessTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/single");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = SingleFileContent("cargo-photo.jpg");

        var response = await _client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<FileUploadResultDto>();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await dbContext.UploadedFiles.FirstOrDefaultAsync(f => f.PublicId == result!.PublicId);
        Assert.NotNull(persisted);
        Assert.Equal(result!.SecureUrl, persisted!.SecureUrl);
    }

    /// <summary>A blocked extension (.exe) is rejected with 400 and the standard error envelope.</summary>
    [Fact]
    public async Task UploadSingle_Returns400_ForBlockedExtension()
    {
        var token = await GetAccessTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/single");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = SingleFileContent("payload.exe");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("BLOCKED_FILE_TYPE", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>A file over the 10 MB limit is rejected with 400.</summary>
    [Fact]
    public async Task UploadSingle_Returns400_ForOversizedFile()
    {
        var token = await GetAccessTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/single");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = SingleFileContent("huge.jpg", new byte[11 * 1024 * 1024]);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("FILE_TOO_LARGE", json.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    // --- Delete single ---

    /// <summary>
    /// DELETE /files/{*publicId} accepts a publicId containing "/" characters (folder-namespaced),
    /// proving the catch-all route parameter captures the full id rather than truncating at the
    /// first path segment.
    /// </summary>
    [Fact]
    public async Task DeleteSingle_AcceptsPublicIdContainingSlashes()
    {
        var token = await GetAccessTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/files/freightlink/nested/abc123");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FileDeleteResultDto>();
        Assert.Equal("freightlink/nested/abc123", result!.PublicId);
    }

    /// <summary>Deleting a file through the API also removes its persisted <c>UploadedFiles</c> metadata row.</summary>
    [Fact]
    public async Task DeleteSingle_RemovesPersistedMetadata()
    {
        var token = await GetAccessTokenAsync();
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/single");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        uploadRequest.Content = SingleFileContent("to-delete.jpg");
        var uploadResponse = await _client.SendAsync(uploadRequest);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<FileUploadResultDto>();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/files/{uploaded!.PublicId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(deleteRequest);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await dbContext.UploadedFiles.FirstOrDefaultAsync(f => f.PublicId == uploaded.PublicId);
        Assert.Null(persisted);
    }
}
