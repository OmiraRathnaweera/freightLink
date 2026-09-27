using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.DTOs.PricingConfig;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PricingEstimatorService"/> — the internal
/// <c>POST /internal/pricing/estimate</c> endpoint's estimation logic. Backed by EF Core's InMemory
/// provider, seeding pricing-config reference data through a real <see cref="PricingConfigService"/>
/// wired to the same <see cref="AppDbContext"/> rather than inserting entities directly, so seeded
/// rows go through the same validation/versioning path production requests do.
/// </summary>
public class PricingEstimatorServiceTests
{
    /// <summary>Creates a fresh, isolated InMemory-backed <see cref="AppDbContext"/> for one test.</summary>
    private static Task<AppDbContext> CreateContextAsync() => CreateContextAsync(Guid.NewGuid().ToString());

    /// <summary>
    /// Creates an InMemory-backed <see cref="AppDbContext"/> against a caller-supplied database name,
    /// so the concurrency test can open a second, independent context onto the same underlying data.
    /// </summary>
    private static Task<AppDbContext> CreateContextAsync(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return Task.FromResult(new AppDbContext(options));
    }

    /// <summary>Builds a real <see cref="PricingEstimatorService"/> wired to the given DB context, reusing a real <see cref="PricingConfigService"/> for its config lookups.</summary>
    private static PricingEstimatorService CreateSut(AppDbContext dbContext) => new(dbContext, new PricingConfigService(dbContext));

    /// <summary>Seeds a minimal Admin user row for pricing-config <c>SetByUserId</c> to reference.</summary>
    private static async Task<Guid> SeedAdminUserAsync(AppDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Admin,
            Email = $"admin-{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-hash",
            FullName = "Pricing Admin",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.UserId;
    }

    /// <summary>Seeds a minimal Shipper user row for <c>Load.ShipperUserId</c> to reference.</summary>
    private static async Task<Guid> SeedShipperUserAsync(AppDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-hash",
            FullName = "Jane Shipper",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.UserId;
    }

    /// <summary>Directly inserts a <see cref="Load"/> with the given weight, bypassing <see cref="LoadService.CreateAsync"/>.</summary>
    private static async Task<Load> SeedLoadAsync(AppDbContext dbContext, Guid shipperUserId, decimal weightKg = 100m)
    {
        var now = DateTimeOffset.UtcNow;
        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            CargoDescription = "Seeded cargo",
            WeightKg = weightKg,
            VolumeM3 = 1m,
            PickupAddress = "100 Pickup Street, Colombo",
            PickupLat = 6.9271m,
            PickupLng = 79.8612m,
            DropoffAddress = "200 Dropoff Road, Kandy",
            DropoffLat = 7.2906m,
            DropoffLng = 80.6337m,
            PickupWindowStart = now.AddDays(1),
            PickupWindowEnd = now.AddDays(2),
            Status = LoadStatus.Matched,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Loads.Add(load);
        await dbContext.SaveChangesAsync();
        return load;
    }

    /// <summary>Seeds AutoDiesel fuel price, a wide-open MiniTruck efficiency tier, and a formula config — the full set the estimator needs.</summary>
    private static async Task SeedFullPricingConfigAsync(
        AppDbContext dbContext, Guid adminId,
        decimal pricePerLitre = 350m, decimal fuelConsumption = 15m,
        decimal baseFare = 500m, decimal ratePerKg = 10m, decimal driverCostPerKm = 20m,
        decimal maintenanceAllowancePerKm = 5m, decimal marginPercent = 0.15m)
    {
        var configService = new PricingConfigService(dbContext);

        await configService.CreateFuelPriceRate(new CreateFuelPriceRateDto
        {
            FuelType = FuelType.AutoDiesel,
            PricePerLitre = pricePerLitre,
            Source = "test",
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        }, adminId);

        await configService.CreateVehicleClassEfficiency(new CreateVehicleClassEfficiencyDto
        {
            ClassLabel = VehicleClass.MiniTruck,
            MinPayloadKg = 0m,
            MaxPayloadKg = null,
            MinVolumeM3 = 0m,
            MaxVolumeM3 = null,
            FuelConsumptionLPer100Km = fuelConsumption,
            Source = "test",
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        }, adminId);

        await configService.CreatePricingFormulaConfig(new CreatePricingFormulaConfigDto
        {
            BaseFare = baseFare,
            RatePerKg = ratePerKg,
            DriverCostPerKm = driverCostPerKm,
            MaintenanceAllowancePerKm = maintenanceAllowancePerKm,
            MarginPercent = marginPercent,
            Source = "test",
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        }, adminId);
    }

    private static EstimatePricingRequestDto ValidRequest(Guid loadId, decimal distanceKm = 100m, VehicleClass vehicleClass = VehicleClass.MiniTruck) => new()
    {
        LoadId = loadId,
        SuggestedVehicleClass = vehicleClass,
        DistanceKm = distanceKm
    };

    /// <summary>
    /// Exact-arithmetic check against known seeded inputs. With PricePerLitre=350, FuelConsumption=15,
    /// DriverCostPerKm=20, MaintenanceAllowancePerKm=5, MarginPercent=0.15, BaseFare=500, RatePerKg=10,
    /// WeightKg=100, DistanceKm=100:
    /// ratePerKm = ((350/100)*15 + 20 + 5) * 1.15 = (52.5 + 20 + 5) * 1.15 = 77.5 * 1.15 = 89.125
    /// estimatedPrice = 500 + (100*89.125) + (100*10) = 500 + 8912.5 + 1000 = 10412.5
    /// </summary>
    [Fact]
    public async Task EstimateAsync_ComputesExactPrice_FromKnownInputs()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        await SeedFullPricingConfigAsync(dbContext, adminId);
        var load = await SeedLoadAsync(dbContext, shipperId, weightKg: 100m);
        var sut = CreateSut(dbContext);

        var result = await sut.EstimateAsync(ValidRequest(load.LoadId, distanceKm: 100m));

        Assert.Equal(89.125m, result.RatePerKm);
        Assert.Equal(10412.5m, result.EstimatedPrice);
        Assert.Equal(load.LoadId, result.LoadId);
        Assert.Equal(100m, result.DistanceKm);
        Assert.Equal(VehicleClass.MiniTruck, result.VehicleClass);
        Assert.Equal(10m, result.RatePerKg);
        Assert.Equal(500m, result.BaseFare);
    }

    [Fact]
    public async Task EstimateAsync_PersistsEstimatedPrice_OnTheLoad()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        await SeedFullPricingConfigAsync(dbContext, adminId);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var sut = CreateSut(dbContext);

        var result = await sut.EstimateAsync(ValidRequest(load.LoadId));

        var row = await dbContext.Loads.AsNoTracking().SingleAsync(l => l.LoadId == load.LoadId);
        Assert.Equal(result.EstimatedPrice, row.EstimatedPrice);
    }

    [Fact]
    public async Task EstimateAsync_Throws404_WhenLoadNotFound()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        await SeedFullPricingConfigAsync(dbContext, adminId);
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.EstimateAsync(ValidRequest(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, exception.Code);
    }

    [Fact]
    public async Task EstimateAsync_Throws503_WhenVehicleClassEfficiencyMissing()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);
        // No VehicleClassEfficiency seeded for MediumLorry, only whatever else may exist.
        var configService = new PricingConfigService(dbContext);
        await configService.CreateFuelPriceRate(new CreateFuelPriceRateDto { FuelType = FuelType.AutoDiesel, PricePerLitre = 350m, Source = "test", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) }, adminId);
        await configService.CreatePricingFormulaConfig(new CreatePricingFormulaConfigDto { BaseFare = 500m, RatePerKg = 10m, DriverCostPerKm = 20m, MaintenanceAllowancePerKm = 5m, MarginPercent = 0.15m, Source = "test", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) }, adminId);
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.EstimateAsync(ValidRequest(load.LoadId, vehicleClass: VehicleClass.MediumLorry)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
    }

    [Fact]
    public async Task EstimateAsync_Throws503_WhenFuelPriceMissing()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var configService = new PricingConfigService(dbContext);
        await configService.CreateVehicleClassEfficiency(new CreateVehicleClassEfficiencyDto { ClassLabel = VehicleClass.MiniTruck, MinPayloadKg = 0m, MaxPayloadKg = null, MinVolumeM3 = 0m, MaxVolumeM3 = null, FuelConsumptionLPer100Km = 15m, Source = "test", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) }, adminId);
        await configService.CreatePricingFormulaConfig(new CreatePricingFormulaConfigDto { BaseFare = 500m, RatePerKg = 10m, DriverCostPerKm = 20m, MaintenanceAllowancePerKm = 5m, MarginPercent = 0.15m, Source = "test", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) }, adminId);
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.EstimateAsync(ValidRequest(load.LoadId)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
    }

    [Fact]
    public async Task EstimateAsync_Throws503_WhenFormulaConfigMissing()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var configService = new PricingConfigService(dbContext);
        await configService.CreateFuelPriceRate(new CreateFuelPriceRateDto { FuelType = FuelType.AutoDiesel, PricePerLitre = 350m, Source = "test", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) }, adminId);
        await configService.CreateVehicleClassEfficiency(new CreateVehicleClassEfficiencyDto { ClassLabel = VehicleClass.MiniTruck, MinPayloadKg = 0m, MaxPayloadKg = null, MinVolumeM3 = 0m, MaxVolumeM3 = null, FuelConsumptionLPer100Km = 15m, Source = "test", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) }, adminId);
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.EstimateAsync(ValidRequest(load.LoadId)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
    }

    /// <summary>
    /// If another request commits a change to this load between when this call's context loaded it
    /// and when it saves, the xmin concurrency token catches the lost-update race and this call gets
    /// a 409 instead of silently overwriting the concurrent change — same pattern as
    /// <c>LoadServiceTests.UpdateAsync_Throws409_WhenLoadWasModifiedConcurrently</c>.
    /// </summary>
    [Fact]
    public async Task EstimateAsync_Throws409_WhenLoadWasModifiedConcurrently()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var seedContext = await CreateContextAsync(databaseName);
        var adminId = await SeedAdminUserAsync(seedContext);
        var shipperId = await SeedShipperUserAsync(seedContext);
        await SeedFullPricingConfigAsync(seedContext, adminId);
        var load = await SeedLoadAsync(seedContext, shipperId);

        using var dbContext = await CreateContextAsync(databaseName);
        var sut = CreateSut(dbContext);
        _ = await dbContext.Loads.SingleAsync(l => l.LoadId == load.LoadId);

        using (var concurrentContext = await CreateContextAsync(databaseName))
        {
            var concurrentlyLoadedRow = await concurrentContext.Loads.SingleAsync(l => l.LoadId == load.LoadId);
            concurrentlyLoadedRow.CargoDescription = "Changed by a concurrent request";
            concurrentContext.Entry(concurrentlyLoadedRow).Property<uint>("xmin").CurrentValue = 12345u;
            await concurrentContext.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.EstimateAsync(ValidRequest(load.LoadId)));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal(ErrorCode.LOAD_CONCURRENCY_CONFLICT, exception.Code);
    }

    // --- EstimateForShipperAsync: the Shipper-facing, haversine-based POST /loads/{id}/estimate ---

    /// <summary>
    /// Exact-arithmetic check against known seeded inputs and <see cref="SeedLoadAsync"/>'s fixed
    /// Colombo→Kandy coordinates (haversine distance ≈ 94.34 km) with WeightKg=100:
    /// ratePerKm = ((350/100)*15 + 20 + 5) * 1.15 = 89.125 (same config as <c>EstimateAsync</c>'s test)
    /// estimatedPrice = 500 + (94.34*89.125) + (100*10) = 500 + 8408.0525 + 1000 = 9908.0525
    /// </summary>
    [Fact]
    public async Task EstimateForShipperAsync_ComputesExactPrice_FromHaversineDistanceAndKnownInputs()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        await SeedFullPricingConfigAsync(dbContext, adminId);
        var load = await SeedLoadAsync(dbContext, shipperId, weightKg: 100m);
        var sut = CreateSut(dbContext);

        var result = await sut.EstimateForShipperAsync(load.LoadId, shipperId, UserRole.Shipper);

        Assert.Equal(94.34m, result.DistanceKm);
        Assert.Equal(89.125m, result.RatePerKm);
        Assert.Equal(9908.0525m, result.EstimatedPrice);
        Assert.Equal(load.LoadId, result.LoadId);
        Assert.Equal(VehicleClass.MiniTruck, result.VehicleClass);
        Assert.Equal(10m, result.RatePerKg);
        Assert.Equal(500m, result.BaseFare);
    }

    /// <summary>
    /// Unlike <see cref="PricingEstimatorService.EstimateAsync"/>, this rough Shipper preview must
    /// never write to <c>Load.EstimatedPrice</c> — that column belongs to the AI agent's own estimate.
    /// </summary>
    [Fact]
    public async Task EstimateForShipperAsync_DoesNotPersistEstimatedPrice_OnTheLoad()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        await SeedFullPricingConfigAsync(dbContext, adminId);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var sut = CreateSut(dbContext);

        await sut.EstimateForShipperAsync(load.LoadId, shipperId, UserRole.Shipper);

        var row = await dbContext.Loads.AsNoTracking().SingleAsync(l => l.LoadId == load.LoadId);
        Assert.Null(row.EstimatedPrice);
    }

    [Fact]
    public async Task EstimateForShipperAsync_Throws404_WhenLoadNotFound()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        await SeedFullPricingConfigAsync(dbContext, adminId);
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.EstimateForShipperAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Shipper));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, exception.Code);
    }

    [Fact]
    public async Task EstimateForShipperAsync_Throws403_ForNonOwningShipper()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        await SeedFullPricingConfigAsync(dbContext, adminId);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var otherShipperId = await SeedShipperUserAsync(dbContext);
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.EstimateForShipperAsync(load.LoadId, otherShipperId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, exception.Code);
    }

    [Fact]
    public async Task EstimateForShipperAsync_Throws403_ForNonShipperRole()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        await SeedFullPricingConfigAsync(dbContext, adminId);
        var load = await SeedLoadAsync(dbContext, shipperId);
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.EstimateForShipperAsync(load.LoadId, shipperId, UserRole.Admin));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, exception.Code);
    }

    [Fact]
    public async Task EstimateForShipperAsync_Throws503_WhenNoVehicleClassTierMatches()
    {
        using var dbContext = await CreateContextAsync();
        var adminId = await SeedAdminUserAsync(dbContext);
        var shipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperId);
        // No VehicleClassEfficiency seeded at all, so no tier can cover the load's weight/volume.
        var configService = new PricingConfigService(dbContext);
        await configService.CreateFuelPriceRate(new CreateFuelPriceRateDto { FuelType = FuelType.AutoDiesel, PricePerLitre = 350m, Source = "test", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) }, adminId);
        await configService.CreatePricingFormulaConfig(new CreatePricingFormulaConfigDto { BaseFare = 500m, RatePerKg = 10m, DriverCostPerKm = 20m, MaintenanceAllowancePerKm = 5m, MarginPercent = 0.15m, Source = "test", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) }, adminId);
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.EstimateForShipperAsync(load.LoadId, shipperId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
    }
}
