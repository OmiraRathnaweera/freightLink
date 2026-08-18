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

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PricingConfigService"/> covering the ADR-019 pricing-config reference
/// tables: current/history reads, weight-tier resolution, append-only creation, the weight-band
/// overlap/gap check, and soft delete. Backed by EF Core's InMemory provider.
/// </summary>
public class PricingConfigServiceTests
{
    /// <summary>Creates a fresh, isolated InMemory-backed <see cref="AppDbContext"/> for one test.</summary>
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

    private static CreateVehicleClassEfficiencyDto ValidVehicleClassEfficiencyDto(
        VehicleClass classLabel = VehicleClass.MiniTruck, decimal minPayloadKg = 0m, decimal? maxPayloadKg = null,
        decimal fuelConsumption = 15m, DateTimeOffset? effectiveFrom = null) => new()
    {
        ClassLabel = classLabel,
        MinPayloadKg = minPayloadKg,
        MaxPayloadKg = maxPayloadKg,
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
    public async Task GetFuelPriceHistory_ReturnsAllRowsIncludingDeleted_NewestFirst()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        var now = DateTimeOffset.UtcNow;

        var first = await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(pricePerLitre: 300m, effectiveFrom: now.AddDays(-2)), adminId);
        var second = await sut.CreateFuelPriceRate(ValidFuelPriceRateDto(pricePerLitre: 320m, effectiveFrom: now.AddDays(-1)), adminId);
        await sut.SoftDeleteFuelPriceRate(first.FuelPriceRateId, adminId);

        var history = await sut.GetFuelPriceHistory(FuelType.AutoDiesel);

        Assert.Equal(2, history.Count);
        Assert.Equal(second.FuelPriceRateId, history[0].FuelPriceRateId);
        Assert.NotNull(history.Single(h => h.FuelPriceRateId == first.FuelPriceRateId).DeletedAt);
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
    public async Task GetAllCurrentFuelPrices_ReturnsEmptyList_WhenNoneConfigured()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var result = await sut.GetAllCurrentFuelPrices();

        Assert.Empty(result);
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
    public async Task GetTierForWeight_ReturnsMatchingTier()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MiniTruck, 0m, 1000m), adminId);
        await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(VehicleClass.MediumLorry, 1000m, null), adminId);

        var result = await sut.GetTierForWeight(1500m);

        Assert.Equal(VehicleClass.MediumLorry, result.ClassLabel);
    }

    [Fact]
    public async Task GetTierForWeight_ThrowsPricingConfigMissing_WhenNoTierMatches()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.GetTierForWeight(500m));

        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
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

    [Fact]
    public async Task SoftDeleteVehicleClassEfficiency_Throws_WhenAlreadyDeleted()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);
        var created = await sut.CreateVehicleClassEfficiency(ValidVehicleClassEfficiencyDto(), adminId);
        await sut.SoftDeleteVehicleClassEfficiency(created.VehicleClassEfficiencyId, adminId);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.SoftDeleteVehicleClassEfficiency(created.VehicleClassEfficiencyId, adminId));

        Assert.Equal(ErrorCode.VEHICLE_CLASS_EFFICIENCY_ALREADY_DELETED, exception.Code);
    }

    [Fact]
    public async Task SoftDeleteVehicleClassEfficiency_Throws_WhenNotFound()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var adminId = await SeedAdminUserAsync(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.SoftDeleteVehicleClassEfficiency(Guid.NewGuid(), adminId));

        Assert.Equal(ErrorCode.VEHICLE_CLASS_EFFICIENCY_NOT_FOUND, exception.Code);
    }
}
