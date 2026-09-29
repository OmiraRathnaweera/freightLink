using FreightLink.Api.Data;
using FreightLink.Api.Entities;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Real-PostgreSQL counterpart to <see cref="CustomWebApplicationFactory"/>: spins up an ephemeral
/// Postgres container via Testcontainers and points <c>ConnectionStrings:DefaultConnection</c> at
/// it, instead of swapping in <c>UseInMemoryDatabase</c>. Unlike <see cref="CustomWebApplicationFactory"/>,
/// this factory does NOT force the <c>Production</c> environment — it uses <c>Development</c> so
/// <c>Program.cs</c>'s <c>if (!app.Environment.IsProduction()) db.Database.Migrate();</c> branch
/// actually runs the real EF Core migrations against the container, giving genuine coverage of
/// "does the full migration chain apply cleanly to Postgres" and "do real DB-level constraints fire"
/// — neither of which the InMemory-backed factory can exercise.
/// </summary>
public class PostgresWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("freightlink_test")
        .WithUsername("freightlink_test")
        .WithPassword("freightlink_test")
        .Build();

    /// <summary>The known <c>INTERNAL_API_KEY</c> value set by this factory, mirroring <see cref="CustomWebApplicationFactory.ValidInternalApiKey"/>.</summary>
    public const string ValidInternalApiKey = "postgres-integration-test-internal-api-key";

    public async Task InitializeAsync()
    {
        // Same rationale as CustomWebApplicationFactory: Program.cs reads JWT/admin-seed/internal-key
        // settings straight from process environment variables, so set them before the host builds.
        Environment.SetEnvironmentVariable("JWT__ISSUER", "FreightLinkApi");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "FreightLinkClient");
        Environment.SetEnvironmentVariable("JWT__KEY", "postgres-integration-test-signing-key-that-is-long-enough-1234567890");
        Environment.SetEnvironmentVariable("JWT__ACCESSTOKENMINUTES", "15");
        Environment.SetEnvironmentVariable("JWT__REFRESHTOKENDAYS", "7");
        // Left unset (unlike CustomWebApplicationFactory) so AuthService.SeedAdminIfNotExistsAsync
        // no-ops (logs a warning and returns) instead of inserting an admin row we don't need.
        Environment.SetEnvironmentVariable("ADMIN_USER_EMAIL", null);
        Environment.SetEnvironmentVariable("ADMIN_USER_PASSWORD", null);
        Environment.SetEnvironmentVariable("INTERNAL_API_KEY", ValidInternalApiKey);
        Environment.SetEnvironmentVariable("EMAIL__ENABLED", "false");
        Environment.SetEnvironmentVariable("EMAIL__REQUIREEMAILVERIFICATION", "false");
        Environment.SetEnvironmentVariable("CORS_ORIGINS", "");

        await _container.StartAsync();

        // Program.cs reads the connection string via ConnectionStrings:DefaultConnection
        // (== CONNECTIONSTRINGS__DEFAULTCONNECTION); set it only after the container has a
        // live port assigned.
        Environment.SetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTION", _container.GetConnectionString());

        // Apply the real migration chain directly (the same Database.Migrate() call Program.cs
        // makes at startup) so every test using this factory has a fully-migrated schema to work
        // against immediately, without needing to boot the full ASP.NET host first (WebApplicationFactory
        // only starts the host lazily on first CreateClient()/Server access, which most of these
        // tests never call — they talk to Postgres directly via CreateDbContext()).
        await using var migrationContext = CreateDbContext();
        await migrationContext.Database.MigrateAsync();
    }

    /// <summary>
    /// Re-applies (a no-op if already up to date) the migration chain via a fresh context — exposed
    /// so <c>MigrationTests</c> can assert directly on the act of migrating, independent of the
    /// automatic migration already performed in <see cref="InitializeAsync"/>.
    /// </summary>
    public async Task MigrateAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Deliberately NOT "Production" — see class remarks. This lets Program.cs's own
        // Database.Migrate() call apply the real migration chain to the container.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // The real Npgsql AppDbContext (pointed at the container via the connection string
            // env var above) is left in place — this factory intentionally does NOT replace it
            // with UseInMemoryDatabase. Only file storage is swapped, same as CustomWebApplicationFactory,
            // since no test here exercises real Cloudinary uploads.
            var fileStorageDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFileStorageService));
            if (fileStorageDescriptor is not null)
            {
                services.Remove(fileStorageDescriptor);
            }

            services.AddScoped<IFileStorageService, FakeFileStorageService>();
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>Opens a fresh <see cref="AppDbContext"/> against the running container, for tests that need direct DB access (e.g. bypassing app-level pre-checks to trigger a real constraint violation).</summary>
    public AppDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(_container.GetConnectionString());
        return new AppDbContext(optionsBuilder.Options);
    }
}
