using FreightLink.Api.Data;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-pipeline test host for <c>AuthController</c>: forces the <c>Production</c> environment
/// (so <c>Program.cs</c> skips <c>Database.Migrate()</c>, which EF Core's InMemory provider
/// doesn't support) and swaps the real Npgsql <c>AppDbContext</c> for an isolated InMemory one.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>
    /// Sets deterministic JWT env vars (so the JwtBearer handler has a valid signing key without
    /// a real <c>.env</c> file) and clears admin-seed credentials so tests start from a clean slate.
    /// </summary>
    public CustomWebApplicationFactory()
    {
        // Program.cs reads JWT settings and admin-seed credentials straight from process
        // environment variables (via DotNetEnv locally); set them here so the test host
        // has a valid signing key without needing a real .env file.
        Environment.SetEnvironmentVariable("JWT__ISSUER", "FreightLinkApi");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "FreightLinkClient");
        Environment.SetEnvironmentVariable("JWT__KEY", "integration-test-signing-key-that-is-long-enough-1234567890");
        Environment.SetEnvironmentVariable("JWT__ACCESSTOKENMINUTES", "15");
        Environment.SetEnvironmentVariable("JWT__REFRESHTOKENDAYS", "7");
        Environment.SetEnvironmentVariable("ADMIN_USER_EMAIL", null);
        Environment.SetEnvironmentVariable("ADMIN_USER_PASSWORD", null);
    }

    /// <summary>Forces the Production environment and swaps in an InMemory <see cref="AppDbContext"/>.</summary>
    /// <param name="builder">The host builder to configure.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Production" skips Program.cs's Database.Migrate() call, which the EF Core
        // InMemory provider used below does not support.
        builder.UseEnvironment("Production");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            // Swap the real Cloudinary-backed storage for an in-memory fake so FilesController
            // integration tests exercise routing/auth/validation without a real network call.
            var fileStorageDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFileStorageService));
            if (fileStorageDescriptor is not null)
            {
                services.Remove(fileStorageDescriptor);
            }

            services.AddScoped<IFileStorageService, FakeFileStorageService>();
        });
    }
}
