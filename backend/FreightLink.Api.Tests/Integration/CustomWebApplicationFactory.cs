using FreightLink.Api.Data;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        // A known, fixed value so InternalPricingControllerTests can exercise both the correct-key
        // and wrong/missing-key paths against a predictable expectation.
        Environment.SetEnvironmentVariable("INTERNAL_API_KEY", "integration-test-internal-api-key");

        // Disabled so Program.cs's EmailOptions.Validate() fail-fast never fires during integration
        // tests — no test here exercises real email sending, so no EMAIL__* settings are needed.
        Environment.SetEnvironmentVariable("EMAIL__ENABLED", "false");
    }

    /// <summary>The known <c>INTERNAL_API_KEY</c> value set by this factory, for tests to send as <c>X-Internal-Api-Key</c>.</summary>
    public const string ValidInternalApiKey = "integration-test-internal-api-key";

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

    /// <summary>
    /// Seeds default pricing reference data (one current <see cref="FuelPriceRate"/>, one wide-open
    /// <see cref="VehicleClassEfficiency"/> tier — wide-open on both payload and volume — and one
    /// current <see cref="PricingFormulaConfig"/>) into the InMemory database right after the host is
    /// built. This data is not consumed by <c>LoadService</c> — Load creation/editing does not depend
    /// on it — it exists so <c>AdminPricingControllerTests</c>' and <c>InternalPricingControllerTests</c>'
    /// endpoint tests have a baseline row to exercise against.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var pricingSeedUserId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            UserId = pricingSeedUserId,
            Role = UserRole.Admin,
            Email = $"pricing-seed-{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-hash",
            FullName = "Pricing Seed Admin",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.FuelPriceRates.Add(new FuelPriceRate
        {
            FuelPriceRateId = Guid.NewGuid(),
            FuelType = FuelType.AutoDiesel,
            PricePerLitre = 350m,
            Source = "test-seed",
            EffectiveFrom = now,
            SetByUserId = pricingSeedUserId,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.VehicleClassEfficiencies.Add(new VehicleClassEfficiency
        {
            VehicleClassEfficiencyId = Guid.NewGuid(),
            ClassLabel = VehicleClass.MiniTruck,
            MinPayloadKg = 0m,
            // Finite, not open-ended: AdminPricingControllerTests adds further classes/bands starting
            // at this ceiling in its own per-test isolated database, which would otherwise be
            // impossible against an open-ended (null) top tier — a band can never legally follow one.
            MaxPayloadKg = 100_000m,
            // Volume band mirrors the payload band's numbers (same reasoning: leaves room for further
            // classes to be appended contiguously in per-test databases).
            MinVolumeM3 = 0m,
            MaxVolumeM3 = 100_000m,
            FuelConsumptionLPer100Km = 15m,
            Source = "test-seed",
            EffectiveFrom = now,
            SetByUserId = pricingSeedUserId,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.PricingFormulaConfigs.Add(new PricingFormulaConfig
        {
            PricingFormulaConfigId = Guid.NewGuid(),
            BaseFare = 500m,
            RatePerKg = 10m,
            DriverCostPerKm = 20m,
            MaintenanceAllowancePerKm = 5m,
            MarginPercent = 0.15m,
            Source = "test-seed",
            EffectiveFrom = now,
            SetByUserId = pricingSeedUserId,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.SaveChanges();

        return host;
    }
}
