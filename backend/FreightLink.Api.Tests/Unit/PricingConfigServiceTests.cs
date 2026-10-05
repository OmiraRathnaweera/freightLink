using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.PricingConfig;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="PricingConfigService"/> covering the ADR-019 pricing-config reference
/// tables: current/history reads, weight-tier resolution, append-only creation, the weight-band
/// overlap/gap check, and soft delete. Backed by EF Core's InMemory provider.
/// </summary>
public class PricingConfigServiceTests
{
    /// <summary>Creates a fresh, isolated InMemory-backed <see cref="AppDbContext"/> for one test.</summary>
    private static AppDbContext CreateContext() => CreateContext(Guid.NewGuid().ToString());

    /// <summary>
    /// Creates an InMemory-backed <see cref="AppDbContext"/> against a caller-supplied database name, so
    /// concurrency tests can open a second, independent context onto the same underlying data.
    /// </summary>
    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Builds a real <see cref="PricingConfigService"/> wired to the given DB context.</summary>
    private static PricingConfigService CreateSut(AppDbContext dbContext) => new(dbContext);

    /// <summary>Seeds a minimal Admin user row for <c>SetByUserId</c>/<c>DeletedByUserId</c> to reference.</summary>
    private static async Task<Guid> SeedAdminUserAsync(AppDbContext dbContext, string fullName = "Pricing Admin")
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Admin,
            Email = $"admin-{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-hash",
            FullName = fullName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.UserId;
    }

    private static CreateFuelPriceRateDto ValidFuelPriceRateDto(FuelType fuelType = FuelType.AutoDiesel, decimal pricePerLitre = 350m, DateTimeOffset? effectiveFrom = null) => new()
    {
        FuelType = fuelType,
        PricePerLitre = pricePerLitre,
        Source = "test",
        EffectiveFrom = effectiveFrom ?? DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Builds a valid create payload. <paramref name="minVolumeM3"/>/<paramref name="maxVolumeM3"/>
    /// default to mirroring <paramref name="minPayloadKg"/>/<paramref name="maxPayloadKg"/> (same
    /// numbers, different unit) so tests that only care about the weight dimension get an
    /// automatically-contiguous volume band too, without needing to pass volume bounds explicitly.
    /// </summary>
    private static CreateVehicleClassEfficiencyDto ValidVehicleClassEfficiencyDto(
        VehicleClass classLabel = VehicleClass.MiniTruck, decimal minPayloadKg = 0m, decimal? maxPayloadKg = null,
        decimal fuelConsumption = 15m, DateTimeOffset? effectiveFrom = null,
        decimal? minVolumeM3 = null, decimal? maxVolumeM3 = null) => new()
    {
        ClassLabel = classLabel,
        MinPayloadKg = minPayloadKg,
        MaxPayloadKg = maxPayloadKg,
        MinVolumeM3 = minVolumeM3 ?? minPayloadKg,
        MaxVolumeM3 = maxVolumeM3 ?? maxPayloadKg,
        FuelConsumptionLPer100Km = fuelConsumption,
        Source = "test",
        EffectiveFrom = effectiveFrom ?? DateTimeOffset.UtcNow
    };

    // --- FuelPriceRate ---

    [Fact]
    public async Task CreateFuelPriceRate_InsertsNewRow_AndReturnsSetByUserName()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        var result = await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(), adminId);

        Assert.NotEqual(Guid.Empty, result.FuelPriceRateId);
        Assert.Equal(adminId, result.SetByUserId);
        Assert.Equal("Pricing Admin", result.SetByUserName);
        Assert.Null(result.DeletedAt);
        Assert.Single(dbContext.FuelPriceRates);
    }

    [Fact]
    public async Task GetCurrentFuelPrice_ReturnsLatestNonDeletedRow()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        var now = DateTimeOffset.UtcNow;

        await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(pricePerLitre: 300m, effectiveFrom: now.AddDays(-2)), adminId);
        await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(pricePerLitre: 320m, effectiveFrom: now.AddDays(-1)), adminId);

        var result = await sut.GetCurrentFuelPrice(FuelType.AutoDiesel);

        Assert.Equal(320m, result.PricePerLitre);
    }

    [Fact]
    public async Task GetCurrentFuelPrice_ThrowsPricingConfigMissing_WhenNoneExist()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.GetCurrentFuelPrice(FuelType.AutoDiesel));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
    }

    [Fact]
    public async Task GetCurrentFuelPrice_IgnoresSoftDeletedRow()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        var created = await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(), adminId);
        await sut.SoftDeleteFuelPriceRate(created.FuelPriceRateId, adminId);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.GetCurrentFuelPrice(FuelType.AutoDiesel));

        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
    }

    [Fact]
    public async Task GetAllCurrentFuelPrices_ReturnsOneRowPerFuelType()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(FuelType.AutoDiesel), adminId);
        await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(FuelType.Petrol92), adminId);

        var result = await sut.GetAllCurrentFuelPrices();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SoftDeleteFuelPriceRate_ReturnsSuccessMessage_AndSetsDeletedFieldsOnTheRow()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        var created = await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(), adminId);

        var result = await sut.SoftDeleteFuelPriceRate(created.FuelPriceRateId, adminId);

        Assert.Equal(created.FuelPriceRateId, result.Id);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));

        var row = await dbContext.FuelPriceRates.AsNoTracking().SingleAsync(x => x.FuelPriceRateId == created.FuelPriceRateId);
        Assert.NotNull(row.DeletedAt);
        Assert.Equal(adminId, row.DeletedByUserId);
    }

    [Fact]
    public async Task SoftDeleteFuelPriceRate_Throws_WhenAlreadyDeleted()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        var created = await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(), adminId);
        await sut.SoftDeleteFuelPriceRate(created.FuelPriceRateId, adminId);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.SoftDeleteFuelPriceRate(created.FuelPriceRateId, adminId));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.Equal(ErrorCode.FUEL_PRICE_RATE_ALREADY_DELETED, exception.Code);
    }

    [Fact]
    public async Task SoftDeleteFuelPriceRate_Throws_WhenNotFound()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.SoftDeleteFuelPriceRate(Guid.NewGuid(), adminId));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(ErrorCode.FUEL_PRICE_RATE_NOT_FOUND, exception.Code);
    }

    /// <summary>A row whose EffectiveFrom is in the future must not become "current" the moment it's inserted.</summary>
    [Fact]
    public async Task GetCurrentFuelPrice_IgnoresFutureDatedRow()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        var now = DateTimeOffset.UtcNow;
        await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(pricePerLitre: 300m, effectiveFrom: now.AddDays(-1)), adminId);
        await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(pricePerLitre: 999m, effectiveFrom: now.AddDays(30)), adminId);

        var result = await sut.GetCurrentFuelPrice(FuelType.AutoDiesel);

        Assert.Equal(300m, result.PricePerLitre);
    }

    /// <summary>
    /// Same in-process lock also serializes soft-deletes: two concurrent soft-deletes of the same row
    /// resolve deterministically (the second sees the first's committed DeletedAt and gets the existing
    /// 422 ALREADY_DELETED), instead of the second silently overwriting the first's DeletedByUserId.
    /// </summary>
    [Fact]
    public async Task SoftDeleteFuelPriceRate_ConcurrentDeletesOfSameRow_SecondGetsAlreadyDeleted()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var dbContext1 = CreateContext(databaseName);
        using var dbContext2 = CreateContext(databaseName);
        var sut1 = CreateSut(dbContext1);
        var sut2 = CreateSut(dbContext2);
        var adminId = await SeedAdminUserAsync(dbContext1);
        var created = await sut1.CreateFuelPriceRate(ValidFuelPriceRateDto(), adminId);
        var otherAdminId = await SeedAdminUserAsync(dbContext2, "Second Admin");

        var task1 = sut1.SoftDeleteFuelPriceRate(created.FuelPriceRateId, adminId);
        var task2 = sut2.SoftDeleteFuelPriceRate(created.FuelPriceRateId, otherAdminId);

        var results = await Task.WhenAll(task1.ContinueWith(TranslateDeleteOutcome), task2.ContinueWith(TranslateDeleteOutcome));

        Assert.Single(results, r => r.Succeeded);
        Assert.Single(results, r => !r.Succeeded && r.ErrorCode == ErrorCode.FUEL_PRICE_RATE_ALREADY_DELETED);
    }

    private static (bool Succeeded, ErrorCode? ErrorCode) TranslateOutcome(Task<VehicleClassEfficiencyResponseDto> task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return (true, null);
        }

        var exception = Assert.IsType<ApiException>(task.Exception!.InnerException);
        return (false, exception.Code);
    }

    private static (bool Succeeded, ErrorCode? ErrorCode) TranslateDeleteOutcome(Task<PricingConfigDeleteResponseDto> task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return (true, null);
        }

        var exception = Assert.IsType<ApiException>(task.Exception!.InnerException);
        return (false, exception.Code);
    }

    private static CreatePricingFormulaConfigDto ValidPricingFormulaConfigDto(
        decimal baseFare = 500m, decimal ratePerKg = 10m, decimal driverCostPerKm = 20m,
        decimal maintenanceAllowancePerKm = 5m, decimal marginPercent = 0.15m, DateTimeOffset? effectiveFrom = null) => new()
    {
        BaseFare = baseFare,
        RatePerKg = ratePerKg,
        DriverCostPerKm = driverCostPerKm,
        MaintenanceAllowancePerKm = maintenanceAllowancePerKm,
        MarginPercent = marginPercent,
        Source = "test",
        EffectiveFrom = effectiveFrom ?? DateTimeOffset.UtcNow
    };

    // --- PricingFormulaConfig ---
    // (Structurally the same Create/GetCurrent/History/SoftDelete pattern as FuelPriceRate above —
    // only the happy-path create and the not-found guard are kept here to avoid re-proving the same
    // current-row-selection/soft-delete mechanics twice.)

    [Fact]
    public async Task CreatePricingFormulaConfig_InsertsNewRow_AndReturnsSetByUserName()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        var result = await sut.CreatePricingFormulaConfig(ValidPricingFormulaConfigDto(), adminId);

        Assert.NotEqual(Guid.Empty, result.PricingFormulaConfigId);
        Assert.Equal(adminId, result.SetByUserId);
        Assert.Equal("Pricing Admin", result.SetByUserName);
        Assert.Null(result.DeletedAt);
        Assert.Single(dbContext.PricingFormulaConfigs);
    }

    [Fact]
    public async Task SoftDeletePricingFormulaConfig_Throws_WhenNotFound()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.SoftDeletePricingFormulaConfig(Guid.NewGuid(), adminId));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(ErrorCode.PRICING_FORMULA_CONFIG_NOT_FOUND, exception.Code);
    }

    /// <summary>
    /// The combined snapshot read PricingEstimatorService uses instead of three separate calls
    /// returns the current value from all three tables together.
    /// </summary>
    [Fact]
    public async Task GetPricingSnapshotForEstimate_ReturnsCurrentValuesFromAllThreeTables()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(pricePerLitre: 355m), adminId);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(fuelConsumption: 12m), adminId);
        await sut.CreatePricingFormulaConfig(ValidPricingFormulaConfigDto(baseFare: 600m), adminId);

        var snapshot = await sut.GetPricingSnapshotForEstimate(VehicleClass.MiniTruck, FuelType.AutoDiesel);

        Assert.Equal(355m, snapshot.FuelPrice.PricePerLitre);
        Assert.Equal(12m, snapshot.Efficiency.FuelConsumptionLPer100Km);
        Assert.Equal(600m, snapshot.FormulaConfig.BaseFare);
    }

    /// <summary>If any one of the three tables has no current row, the whole snapshot fails loudly rather than returning a partial result.</summary>
    [Fact]
    public async Task GetPricingSnapshotForEstimate_Throws_WhenFormulaConfigMissing()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(), adminId);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(), adminId);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.GetPricingSnapshotForEstimate(VehicleClass.MiniTruck, FuelType.AutoDiesel));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
    }

    // --- VehicleClassEfficiency ---

    [Fact]
    public async Task CreateVehicleClassEfficiency_FirstTier_MustStartAtZero()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(minPayloadKg: 100m), adminId));

        Assert.Equal(ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_GAP, exception.Code);
    }

    [Fact]
    public async Task CreateVehicleClassEfficiency_Succeeds_ForContiguousBands()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MiniTruck, 0m, 1000m), adminId);
        var result = await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MediumLorry, 1000m, null), adminId);

        Assert.Equal(1000m, result.MinPayloadKg);
        Assert.Null(result.MaxPayloadKg);
    }

    [Fact]
    public async Task CreateVehicleClassEfficiency_Throws_OnOverlap()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MiniTruck, 0m, 1000m), adminId);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MediumLorry, 500m, null), adminId));

        Assert.Equal(ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP, exception.Code);
    }

    [Fact]
    public async Task CreateVehicleClassEfficiency_Throws_OnGap()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MiniTruck, 0m, 1000m), adminId);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MediumLorry, 1500m, null), adminId));

        Assert.Equal(ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_GAP, exception.Code);
    }

    [Fact]
    public async Task CreateVehicleClassEfficiency_Throws_WhenMaxNotGreaterThanMin()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(minPayloadKg: 0m, maxPayloadKg: 0m), adminId));

        Assert.Equal(ErrorCode.VEHICLE_CLASS_EFFICIENCY_INVALID_PAYLOAD_BAND, exception.Code);
    }

    /// <summary>
    /// Re-versioning a class (a later EffectiveFrom row for the SAME ClassLabel) must not be rejected
    /// as "overlapping itself" — the class being submitted is excluded from the pre-insert overlap
    /// check against the other classes' current bands.
    /// </summary>
    [Fact]
    public async Task CreateVehicleClassEfficiency_NewVersion_ExcludesOwnPriorRowFromOverlapCheck()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        var now = DateTimeOffset.UtcNow;

        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MiniTruck, 0m, 1000m, effectiveFrom: now.AddDays(-1)), adminId);
        var result = await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MiniTruck, 0m, 1000m, effectiveFrom: now), adminId);

        Assert.Equal(0m, result.MinPayloadKg);
    }

    [Fact]
    public async Task GetTierForWeightAndVolume_ReturnsMatchingTier()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MiniTruck, 0m, 1000m), adminId);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MediumLorry, 1000m, null), adminId);

        var result = await sut.GetTierForWeightAndVolume(1500m, 1500m);

        Assert.Equal(VehicleClass.MediumLorry, result.ClassLabel);
    }

    [Fact]
    public async Task GetTierForWeightAndVolume_ThrowsPricingConfigMissing_WhenNoTierMatches()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.GetTierForWeightAndVolume(500m, 5m));

        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
    }

    /// <summary>
    /// A light-but-bulky load must be upsized to the tier its volume demands, even though its weight
    /// alone would match a smaller tier — the fix for the "bulky loads get an inappropriate weight-only
    /// tier" bug. MiniTruck's payload band [0, 1000) covers 200kg easily, but its volume band [0, 5)
    /// does not cover 15 m³; MediumLorry's volume band [5, null) does.
    /// </summary>
    [Fact]
    public async Task GetTierForWeightAndVolume_UpsizesToLargerTier_WhenVolumeDemandsABiggerClassThanWeight()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MiniTruck, 0m, 1000m, minVolumeM3: 0m, maxVolumeM3: 5m), adminId);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MediumLorry, 1000m, null, minVolumeM3: 5m, maxVolumeM3: null), adminId);

        var result = await sut.GetTierForWeightAndVolume(200m, 15m);

        Assert.Equal(VehicleClass.MediumLorry, result.ClassLabel);
    }

    /// <summary>
    /// Two concurrent CreateVehicleClassEfficiency calls, both claiming the full open [0,∞) band for two
    /// different classes on an empty table, must not both succeed — the static in-process lock
    /// serializes them, so the second call re-validates against the first's now-committed state and
    /// deterministically loses with a band-overlap error, instead of both racing the same stale
    /// (empty) snapshot and both committing an overlapping "current" band.
    /// </summary>
    [Fact]
    public async Task CreateVehicleClassEfficiency_ConcurrentCreatesForDifferentClasses_OnlyOneSucceeds()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var dbContext1 = CreateContext(databaseName);
        using var dbContext2 = CreateContext(databaseName);
        var sut1 = CreateSut(dbContext1);
        var sut2 = CreateSut(dbContext2);
        var adminId = await SeedAdminUserAsync(dbContext1);

        var task1 = sut1.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MiniTruck, 0m, null), adminId);
        var task2 = sut2.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MediumLorry, 0m, null), adminId);

        var results = await Task.WhenAll(task1.ContinueWith(TranslateOutcome), task2.ContinueWith(TranslateOutcome));

        Assert.Single(results, r => r.Succeeded);
        Assert.Single(results, r => !r.Succeeded && r.ErrorCode == ErrorCode.VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP);
    }

    [Fact]
    public async Task SoftDeleteVehicleClassEfficiency_ReturnsSuccessMessage_AndSetsDeletedFieldsOnTheRow()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        var created = await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(), adminId);

        var result = await sut.SoftDeleteVehicleClassEfficiency(created.VehicleClassEfficiencyId, adminId);

        Assert.Equal(created.VehicleClassEfficiencyId, result.Id);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));

        var row = await dbContext.VehicleClassEfficiencies.AsNoTracking().SingleAsync(x => x.VehicleClassEfficiencyId == created.VehicleClassEfficiencyId);
        Assert.NotNull(row.DeletedAt);
        Assert.Equal(adminId, row.DeletedByUserId);
    }
    // ---- DEF-004: startup default-pricing seed must never reference a non-existent Admin ----

    /// <summary>With no Admin user the seed must skip entirely - a made-up SetByUserId violates the real FK on PostgreSQL and crashed startup.</summary>
    [Fact]
    public async Task SeedDefaultPricingConfig_WithNoAdminUser_SeedsNothing()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        await sut.SeedDefaultPricingConfigIfNotExistsAsync();

        Assert.Empty(await dbContext.FuelPriceRates.ToListAsync());
        Assert.Empty(await dbContext.PricingFormulaConfigs.ToListAsync());
        Assert.Empty(await dbContext.VehicleClassEfficiencies.ToListAsync());
    }

    /// <summary>With an Admin present, defaults are seeded and attributed to that real Admin.</summary>
    [Fact]
    public async Task SeedDefaultPricingConfig_WithAdminUser_SeedsDefaultsAttributedToAdmin()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        await sut.SeedDefaultPricingConfigIfNotExistsAsync();

        Assert.All(await dbContext.FuelPriceRates.ToListAsync(), x => Assert.Equal(adminId, x.SetByUserId));
        Assert.All(await dbContext.PricingFormulaConfigs.ToListAsync(), x => Assert.Equal(adminId, x.SetByUserId));
        Assert.Equal(3, await dbContext.VehicleClassEfficiencies.CountAsync());
    }

    /// <summary>Running the seed twice must not duplicate rows (idempotent boundary case).</summary>
    [Fact]
    public async Task SeedDefaultPricingConfig_CalledTwice_IsIdempotent()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        await SeedAdminUserAsync(dbContext);

        await sut.SeedDefaultPricingConfigIfNotExistsAsync();
        await sut.SeedDefaultPricingConfigIfNotExistsAsync();

        Assert.Single(await dbContext.FuelPriceRates.ToListAsync());
        Assert.Single(await dbContext.PricingFormulaConfigs.ToListAsync());
        Assert.Equal(3, await dbContext.VehicleClassEfficiencies.CountAsync());
    }
}
