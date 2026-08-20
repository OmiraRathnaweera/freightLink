using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.PricingConfig;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-HTTP-pipeline tests for <c>AdminPricingController</c>, focused on the Admin-only role gating
/// every route enforces and the append-only/soft-delete/overlap-gap behavior exposed at the HTTP layer.
/// Unlike <c>LoadsControllerTests</c>, this class does NOT share one <see cref="CustomWebApplicationFactory"/>
/// (and thus one InMemory database) across all its tests via <c>IClassFixture</c> — pricing-config rows
/// are global, un-partitioned reference data (no per-owner scoping the way Loads have via
/// ShipperUserId), so two tests creating VehicleClassEfficiency rows in the same database could
/// spuriously conflict via the overlap/gap check depending on execution order. Each test instead builds
/// its own short-lived factory, giving it a private, empty InMemory database. Service-level scenario
/// coverage lives in <c>PricingConfigServiceTests</c>.
/// </summary>
public class AdminPricingControllerTests
{
    /// <summary>Registers a new shipper with a unique email and logs in, returning the issued tokens.</summary>
    private static async Task<TokenResponseDto> RegisterAndLoginShipperAsync(HttpClient client, string emailPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecret1";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = email,
            Password = password,
            FullName = "Integration Tester",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    /// <summary>
    /// Seeds a genuine Admin <see cref="User"/> row directly into <paramref name="factory"/>'s InMemory
    /// database, then mints a validly-signed Admin-role JWT whose <c>NameIdentifier</c> claim is that
    /// row's real id. A bare minted token with a random, non-existent user id (the simpler pattern
    /// <c>LoadsControllerTests.MintAdminToken</c> uses, safe there since <c>Load</c> never stores an
    /// Admin id as a foreign key) would leave every <c>SetByUserId</c> this test writes pointing at no
    /// row at all — under EF Core's InMemory provider, <c>.Include(x => x.SetByUser)</c> on that
    /// *required* navigation then silently drops the row entirely (acts like an inner join), which is
    /// exactly what every <see cref="Services.PricingConfigService"/> read path uses.
    /// </summary>
    private static async Task<string> SeedAndMintAdminTokenAsync(CustomWebApplicationFactory factory, string fullName = "Test Admin")
    {
        var adminUserId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;
            dbContext.Users.Add(new User
            {
                UserId = adminUserId,
                Role = UserRole.Admin,
                Email = $"admin-{Guid.NewGuid():N}@example.com",
                PasswordHash = "unused-hash",
                FullName = fullName,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            await dbContext.SaveChangesAsync();
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-that-is-long-enough-1234567890"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, adminUserId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, adminUserId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

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

    private static CreateFuelPriceRateDto ValidFuelPriceRateDto(FuelType fuelType = FuelType.AutoDiesel) => new()
    {
        FuelType = fuelType,
        PricePerLitre = 355m,
        Source = "integration-test",
        EffectiveFrom = DateTimeOffset.UtcNow
    };

    /// <summary>Volume band mirrors the payload band's numbers by default (same reasoning as the seeded MiniTruck tier), so these tests stay focused on the weight dimension without tripping the new volume-overlap/gap check.</summary>
    private static CreateVehicleClassEfficiencyDto ValidVehicleClassEfficiencyDto(VehicleClass classLabel = VehicleClass.MiniTruck, decimal minPayloadKg = 0m, decimal? maxPayloadKg = null) => new()
    {
        ClassLabel = classLabel,
        MinPayloadKg = minPayloadKg,
        MaxPayloadKg = maxPayloadKg,
        MinVolumeM3 = minPayloadKg,
        MaxVolumeM3 = maxPayloadKg,
        FuelConsumptionLPer100Km = 15m,
        Source = "integration-test",
        EffectiveFrom = DateTimeOffset.UtcNow
    };

    private static CreatePricingFormulaConfigDto ValidPricingFormulaConfigDto(decimal baseFare = 600m) => new()
    {
        BaseFare = baseFare,
        RatePerKg = 12m,
        DriverCostPerKm = 22m,
        MaintenanceAllowancePerKm = 6m,
        MarginPercent = 0.2m,
        Source = "integration-test",
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1)
    };

    // --- Pricing formula configuration ---
    // Each factory instance's InMemory database starts with one pre-seeded current configuration
    // (see CustomWebApplicationFactory), so a freshly-created row here (dated later) becomes the new
    // "current" one, and both rows should still show up together in history.

    /// <summary>An Admin can record a new pricing formula configuration; the response is 201.</summary>
    [Fact]
    public async Task CreateFormulaConfig_Returns201_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/formula-config", await SeedAndMintAdminTokenAsync(factory));
        request.Content = JsonContent.Create(ValidPricingFormulaConfigDto());
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PricingFormulaConfigResponseDto>();
        Assert.Equal(600m, created!.BaseFare);
    }

    /// <summary>A Shipper cannot record a pricing formula configuration.</summary>
    [Fact]
    public async Task CreateFormulaConfig_Returns403_ForShipper()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var tokens = await RegisterAndLoginShipperAsync(client, "formula-config-shipper");

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/formula-config", tokens.AccessToken);
        request.Content = JsonContent.Create(ValidPricingFormulaConfigDto());
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>POST /admin/pricing/formula-config without a token is 401.</summary>
    [Fact]
    public async Task CreateFormulaConfig_Returns401_WithoutToken()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/pricing/formula-config", ValidPricingFormulaConfigDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An omitted BaseFare fails DataAnnotations validation automatically — 400 VALIDATION_ERROR, no hand-rolled check.</summary>
    [Fact]
    public async Task CreateFormulaConfig_Returns400_WhenBaseFareMissing()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/formula-config", await SeedAndMintAdminTokenAsync(factory));
        request.Content = JsonContent.Create(new
        {
            ratePerKg = 12m,
            driverCostPerKm = 22m,
            maintenanceAllowancePerKm = 6m,
            marginPercent = 0.2m,
            source = "test",
            effectiveFrom = DateTimeOffset.UtcNow
        });
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>GET /admin/pricing/formula-config returns the current configuration (the factory's seed, if nothing newer was created).</summary>
    [Fact]
    public async Task GetCurrentFormulaConfig_Returns200_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/admin/pricing/formula-config", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = await response.Content.ReadFromJsonAsync<PricingFormulaConfigResponseDto>();
        Assert.NotNull(current);
    }

    /// <summary>GET /admin/pricing/formula-config/history returns every row, including the factory's seed and any newly created one.</summary>
    [Fact]
    public async Task GetFormulaConfigHistory_Returns200_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        PricingFormulaConfigResponseDto created;
        using (var create = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/formula-config", await SeedAndMintAdminTokenAsync(factory)))
        {
            create.Content = JsonContent.Create(ValidPricingFormulaConfigDto());
            var createResponse = await client.SendAsync(create);
            createResponse.EnsureSuccessStatusCode();
            created = (await createResponse.Content.ReadFromJsonAsync<PricingFormulaConfigResponseDto>())!;
        }

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/admin/pricing/formula-config/history", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<List<PricingFormulaConfigResponseDto>>();
        Assert.True(history!.Count >= 2);
        Assert.Contains(history, h => h.PricingFormulaConfigId == created.PricingFormulaConfigId);
    }

    /// <summary>Soft-deleting a pricing formula configuration returns 200 with a success message, not the deleted row, and not 204.</summary>
    [Fact]
    public async Task DeleteFormulaConfig_Returns200WithSuccessMessage_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        PricingFormulaConfigResponseDto created;
        using (var create = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/formula-config", await SeedAndMintAdminTokenAsync(factory)))
        {
            create.Content = JsonContent.Create(ValidPricingFormulaConfigDto());
            var createResponse = await client.SendAsync(create);
            createResponse.EnsureSuccessStatusCode();
            created = (await createResponse.Content.ReadFromJsonAsync<PricingFormulaConfigResponseDto>())!;
        }

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/formula-config/{created.PricingFormulaConfigId}", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PricingConfigDeleteResponseDto>();
        Assert.Equal(created.PricingFormulaConfigId, result!.Id);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    /// <summary>Soft-deleting an already-soft-deleted pricing formula configuration is a 422, not a silent no-op.</summary>
    [Fact]
    public async Task DeleteFormulaConfig_Returns422_WhenAlreadyDeleted()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        PricingFormulaConfigResponseDto created;
        using (var create = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/formula-config", await SeedAndMintAdminTokenAsync(factory)))
        {
            create.Content = JsonContent.Create(ValidPricingFormulaConfigDto());
            var createResponse = await client.SendAsync(create);
            createResponse.EnsureSuccessStatusCode();
            created = (await createResponse.Content.ReadFromJsonAsync<PricingFormulaConfigResponseDto>())!;
        }

        using (var firstDelete = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/formula-config/{created.PricingFormulaConfigId}", await SeedAndMintAdminTokenAsync(factory)))
        {
            (await client.SendAsync(firstDelete)).EnsureSuccessStatusCode();
        }

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/formula-config/{created.PricingFormulaConfigId}", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("PRICING_FORMULA_CONFIG_ALREADY_DELETED", await ReadErrorCodeAsync(response));
    }

    /// <summary>Deleting a nonexistent pricing formula configuration id is 404.</summary>
    [Fact]
    public async Task DeleteFormulaConfig_Returns404_WhenNotFound()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/formula-config/{Guid.NewGuid()}", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("PRICING_FORMULA_CONFIG_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    // --- Fuel rates ---

    /// <summary>An Admin can record a new fuel price rate; the response is 201 with the created resource.</summary>
    [Fact]
    public async Task CreateFuelRate_Returns201_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/fuel-rates", await SeedAndMintAdminTokenAsync(factory));
        request.Content = JsonContent.Create(ValidFuelPriceRateDto());
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<FuelPriceRateResponseDto>();
        Assert.Equal(355m, created!.PricePerLitre);
    }

    /// <summary>A Shipper cannot record a fuel price rate — every AdminPricing route is Admin-only.</summary>
    [Fact]
    public async Task CreateFuelRate_Returns403_ForShipper()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var tokens = await RegisterAndLoginShipperAsync(client, "fuel-rate-shipper");

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/fuel-rates", tokens.AccessToken);
        request.Content = JsonContent.Create(ValidFuelPriceRateDto());
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>POST /admin/pricing/fuel-rates without a token is 401.</summary>
    [Fact]
    public async Task CreateFuelRate_Returns401_WithoutToken()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/pricing/fuel-rates", ValidFuelPriceRateDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An omitted FuelType fails DataAnnotations validation automatically — 400 VALIDATION_ERROR, no hand-rolled check.</summary>
    [Fact]
    public async Task CreateFuelRate_Returns400_WhenFuelTypeMissing()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/fuel-rates", await SeedAndMintAdminTokenAsync(factory));
        request.Content = JsonContent.Create(new { pricePerLitre = 350m, source = "test", effectiveFrom = DateTimeOffset.UtcNow });
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>GET /admin/pricing/fuel-rates lists the current rate for every configured fuel type.</summary>
    [Fact]
    public async Task GetCurrentFuelRates_Returns200_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using (var create = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/fuel-rates", await SeedAndMintAdminTokenAsync(factory)))
        {
            create.Content = JsonContent.Create(ValidFuelPriceRateDto(FuelType.Petrol95));
            (await client.SendAsync(create)).EnsureSuccessStatusCode();
        }

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/admin/pricing/fuel-rates", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rates = await response.Content.ReadFromJsonAsync<List<FuelPriceRateResponseDto>>();
        Assert.Contains(rates!, r => r.FuelType == FuelType.Petrol95);
        // The factory's default pricing seed (AutoDiesel) should also show up alongside the new one.
        Assert.Contains(rates!, r => r.FuelType == FuelType.AutoDiesel);
    }

    /// <summary>GET /admin/pricing/fuel-rates/history?fuelType=... returns every row for that fuel type.</summary>
    [Fact]
    public async Task GetFuelRateHistory_Returns200_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        FuelPriceRateResponseDto created;
        using (var create = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/fuel-rates", await SeedAndMintAdminTokenAsync(factory)))
        {
            create.Content = JsonContent.Create(ValidFuelPriceRateDto(FuelType.SuperDiesel));
            var createResponse = await client.SendAsync(create);
            createResponse.EnsureSuccessStatusCode();
            created = (await createResponse.Content.ReadFromJsonAsync<FuelPriceRateResponseDto>())!;
        }

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/admin/pricing/fuel-rates/history?fuelType=SuperDiesel", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await response.Content.ReadFromJsonAsync<List<FuelPriceRateResponseDto>>();
        Assert.Contains(history!, r => r.FuelPriceRateId == created.FuelPriceRateId);
    }

    /// <summary>An omitted fuelType query parameter on the history route 400s automatically.</summary>
    [Fact]
    public async Task GetFuelRateHistory_Returns400_WhenFuelTypeMissing()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/admin/pricing/fuel-rates/history", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Soft-deleting a fuel rate returns 200 with a success message, not the deleted row, and not 204.</summary>
    [Fact]
    public async Task DeleteFuelRate_Returns200WithSuccessMessage_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        FuelPriceRateResponseDto created;
        using (var create = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/fuel-rates", await SeedAndMintAdminTokenAsync(factory)))
        {
            create.Content = JsonContent.Create(ValidFuelPriceRateDto(FuelType.Petrol92));
            var createResponse = await client.SendAsync(create);
            createResponse.EnsureSuccessStatusCode();
            created = (await createResponse.Content.ReadFromJsonAsync<FuelPriceRateResponseDto>())!;
        }

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/fuel-rates/{created.FuelPriceRateId}", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PricingConfigDeleteResponseDto>();
        Assert.Equal(created.FuelPriceRateId, result!.Id);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    /// <summary>Soft-deleting an already-soft-deleted fuel rate is a 422, not a silent no-op.</summary>
    [Fact]
    public async Task DeleteFuelRate_Returns422_WhenAlreadyDeleted()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        FuelPriceRateResponseDto created;
        using (var create = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/fuel-rates", await SeedAndMintAdminTokenAsync(factory)))
        {
            create.Content = JsonContent.Create(ValidFuelPriceRateDto(FuelType.Petrol92));
            var createResponse = await client.SendAsync(create);
            createResponse.EnsureSuccessStatusCode();
            created = (await createResponse.Content.ReadFromJsonAsync<FuelPriceRateResponseDto>())!;
        }

        using (var firstDelete = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/fuel-rates/{created.FuelPriceRateId}", await SeedAndMintAdminTokenAsync(factory)))
        {
            (await client.SendAsync(firstDelete)).EnsureSuccessStatusCode();
        }

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/fuel-rates/{created.FuelPriceRateId}", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("FUEL_PRICE_RATE_ALREADY_DELETED", await ReadErrorCodeAsync(response));
    }

    /// <summary>Deleting a nonexistent fuel rate id is 404.</summary>
    [Fact]
    public async Task DeleteFuelRate_Returns404_WhenNotFound()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/fuel-rates/{Guid.NewGuid()}", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("FUEL_PRICE_RATE_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    // --- Vehicle-class efficiency ---
    // Each factory instance's InMemory database starts with one pre-seeded MiniTruck tier
    // (0-100,000 kg, see CustomWebApplicationFactory) so Load create/update tests always have a
    // matching band. New bands in these tests are placed starting at 100,000 kg to stay contiguous
    // with, not overlapping, that seed.

    /// <summary>An Admin can record a new vehicle-class efficiency figure; the response is 201.</summary>
    [Fact]
    public async Task CreateVehicleEfficiency_Returns201_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/vehicle-efficiency", await SeedAndMintAdminTokenAsync(factory));
        request.Content = JsonContent.Create(ValidVehicleClassEfficiencyDto(VehicleClass.ContainerTruck, 100_000m, null));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<VehicleClassEfficiencyResponseDto>();
        Assert.Equal(VehicleClass.ContainerTruck, created!.ClassLabel);
    }

    /// <summary>A Shipper cannot record a vehicle-class efficiency figure.</summary>
    [Fact]
    public async Task CreateVehicleEfficiency_Returns403_ForShipper()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var tokens = await RegisterAndLoginShipperAsync(client, "vce-shipper");

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/vehicle-efficiency", tokens.AccessToken);
        request.Content = JsonContent.Create(ValidVehicleClassEfficiencyDto(VehicleClass.ContainerTruck, 100_000m, null));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A payload band overlapping an already-current band is rejected with a 400 and the specific band-overlap code.</summary>
    [Fact]
    public async Task CreateVehicleEfficiency_Returns400_OnBandOverlap()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using (var first = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/vehicle-efficiency", await SeedAndMintAdminTokenAsync(factory)))
        {
            first.Content = JsonContent.Create(ValidVehicleClassEfficiencyDto(VehicleClass.MediumLorry, 100_000m, 200_000m));
            (await client.SendAsync(first)).EnsureSuccessStatusCode();
        }

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/vehicle-efficiency", await SeedAndMintAdminTokenAsync(factory));
        request.Content = JsonContent.Create(ValidVehicleClassEfficiencyDto(VehicleClass.ContainerTruck, 150_000m, null));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP", await ReadErrorCodeAsync(response));
    }

    /// <summary>A payload band leaving a gap against the other current bands is rejected with the specific band-gap code.</summary>
    [Fact]
    public async Task CreateVehicleEfficiency_Returns400_OnBandGap()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/vehicle-efficiency", await SeedAndMintAdminTokenAsync(factory));
        // Leaves a gap between the seeded MiniTruck's ceiling (100,000 kg) and this band's start.
        request.Content = JsonContent.Create(ValidVehicleClassEfficiencyDto(VehicleClass.ContainerTruck, 150_000m, null));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VEHICLE_CLASS_EFFICIENCY_BAND_GAP", await ReadErrorCodeAsync(response));
    }

    /// <summary>GET /admin/pricing/vehicle-efficiency lists the current figure for every configured class.</summary>
    [Fact]
    public async Task GetCurrentVehicleEfficiency_Returns200_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using (var create = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/vehicle-efficiency", await SeedAndMintAdminTokenAsync(factory)))
        {
            create.Content = JsonContent.Create(ValidVehicleClassEfficiencyDto(VehicleClass.ContainerTruck, 100_000m, null));
            (await client.SendAsync(create)).EnsureSuccessStatusCode();
        }

        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/admin/pricing/vehicle-efficiency", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var figures = await response.Content.ReadFromJsonAsync<List<VehicleClassEfficiencyResponseDto>>();
        Assert.Contains(figures!, f => f.ClassLabel == VehicleClass.ContainerTruck);
        // The factory's default pricing seed (MiniTruck) should also show up alongside the new one.
        Assert.Contains(figures!, f => f.ClassLabel == VehicleClass.MiniTruck);
    }

    /// <summary>Soft-deleting a vehicle-class efficiency figure returns 200 with a success message, not the deleted row, and not 204.</summary>
    [Fact]
    public async Task DeleteVehicleEfficiency_Returns200WithSuccessMessage_ForAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        VehicleClassEfficiencyResponseDto created;
        using (var create = AuthedRequest(HttpMethod.Post, "/api/v1/admin/pricing/vehicle-efficiency", await SeedAndMintAdminTokenAsync(factory)))
        {
            create.Content = JsonContent.Create(ValidVehicleClassEfficiencyDto(VehicleClass.MediumLorry, 100_000m, null));
            var createResponse = await client.SendAsync(create);
            createResponse.EnsureSuccessStatusCode();
            created = (await createResponse.Content.ReadFromJsonAsync<VehicleClassEfficiencyResponseDto>())!;
        }

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/vehicle-efficiency/{created.VehicleClassEfficiencyId}", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PricingConfigDeleteResponseDto>();
        Assert.Equal(created.VehicleClassEfficiencyId, result!.Id);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    /// <summary>Deleting a nonexistent vehicle-class efficiency id is 404.</summary>
    [Fact]
    public async Task DeleteVehicleEfficiency_Returns404_WhenNotFound()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/admin/pricing/vehicle-efficiency/{Guid.NewGuid()}", await SeedAndMintAdminTokenAsync(factory));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("VEHICLE_CLASS_EFFICIENCY_NOT_FOUND", await ReadErrorCodeAsync(response));
    }
}
