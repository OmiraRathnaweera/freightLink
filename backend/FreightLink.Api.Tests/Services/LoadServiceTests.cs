using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="LoadService"/> covering create/get/list/edit/cancel and status-transition
/// enforcement. Backed by EF Core's InMemory provider — no real Postgres needed. This is also this
/// project's full "integration test" coverage for <see cref="LoadService"/>, since no controller/HTTP
/// layer exists yet for a <c>WebApplicationFactory</c>-based test to exercise.
/// </summary>
public class LoadServiceTests
{
    /// <summary>Creates a fresh, isolated InMemory-backed <see cref="AppDbContext"/> for one test, seeded with default pricing config (see <see cref="SeedDefaultPricingConfigAsync"/>).</summary>
    private static async Task<AppDbContext> CreateContextAsync(bool seedPricing = true) => await CreateContextAsync(Guid.NewGuid().ToString(), seedPricing);

    /// <summary>
    /// Creates an InMemory-backed <see cref="AppDbContext"/> against a caller-supplied database name,
    /// so concurrency tests can open a second, independent context onto the same underlying data.
    /// Seeds default pricing config unless <paramref name="seedPricing"/> is <c>false</c> — every test
    /// exercising <see cref="LoadService.CreateAsync"/>/<see cref="LoadService.UpdateAsync"/> now needs
    /// pricing config to exist (see <see cref="LoadService.CalculateEstimatedPriceAsync"/>), so this
    /// seeds by default rather than requiring every existing test to opt in individually.
    /// </summary>
    private static async Task<AppDbContext> CreateContextAsync(string databaseName, bool seedPricing = true)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var dbContext = new AppDbContext(options);

        if (seedPricing)
        {
            await SeedDefaultPricingConfigAsync(dbContext);
        }

        return dbContext;
    }

    /// <summary>
    /// Seeds one current <see cref="FuelPriceRate"/> (AutoDiesel) and one wide-open
    /// <see cref="VehicleClassEfficiency"/> tier (<c>MinPayloadKg = 0</c>, <c>MaxPayloadKg = null</c>)
    /// — deliberately a single all-covering tier, not the three real ADR-019 tiers, so every
    /// weight-based test keeps passing without per-test changes.
    /// </summary>
    private static async Task SeedDefaultPricingConfigAsync(AppDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var setByUserId = await SeedShipperUserAsync(dbContext, fullName: "Pricing Admin");

        dbContext.FuelPriceRates.Add(new FuelPriceRate
        {
            FuelPriceRateId = Guid.NewGuid(),
            FuelType = FuelType.AutoDiesel,
            PricePerLitre = 350m,
            Source = "test-seed",
            EffectiveFrom = now,
            SetByUserId = setByUserId,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.VehicleClassEfficiencies.Add(new VehicleClassEfficiency
        {
            VehicleClassEfficiencyId = Guid.NewGuid(),
            ClassLabel = VehicleClass.MiniTruck,
            MinPayloadKg = 0m,
            MaxPayloadKg = null,
            FuelConsumptionLPer100Km = 15m,
            Source = "test-seed",
            EffectiveFrom = now,
            SetByUserId = setByUserId,
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync();
    }

    /// <summary>Builds a real <see cref="LoadService"/> wired to the given DB context.</summary>
    private static LoadService CreateSut(AppDbContext dbContext) => new(dbContext, new PricingConfigService(dbContext));

    /// <summary>Seeds a minimal Shipper user row for <c>Load.ShipperUserId</c> to reference.</summary>
    private static async Task<Guid> SeedShipperUserAsync(AppDbContext dbContext, string fullName = "Jane Shipper")
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
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

    /// <summary>Directly inserts a <see cref="Load"/> already in <paramref name="status"/>, bypassing <see cref="LoadService.CreateAsync"/>, for edit/cancel tests.</summary>
    private static async Task<Load> SeedLoadAsync(AppDbContext dbContext, Guid shipperUserId, LoadStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            CargoDescription = "Seeded cargo",
            WeightKg = 100m,
            VolumeM3 = 1m,
            PickupAddress = "100 Pickup Street, Colombo",
            PickupLat = 6.9271m,
            PickupLng = 79.8612m,
            DropoffAddress = "200 Dropoff Road, Kandy",
            DropoffLat = 7.2906m,
            DropoffLng = 80.6337m,
            PickupWindowStart = now.AddDays(1),
            PickupWindowEnd = now.AddDays(2),
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Loads.Add(load);
        await dbContext.SaveChangesAsync();
        return load;
    }

    /// <summary>A valid Create payload, with an overridable <c>PostImmediately</c> flag.</summary>
    private static CreateLoadDto ValidCreateLoadDto(bool postImmediately = false) => new()
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
        PickupWindowEnd = DateTimeOffset.UtcNow.AddDays(2),
        PostImmediately = postImmediately
    };

    /// <summary>A valid Update payload.</summary>
    private static UpdateLoadDto ValidUpdateLoadDto() => new()
    {
        CargoDescription = "Updated cargo description",
        WeightKg = 750m,
        VolumeM3 = 3m,
        PickupAddress = "123 Pickup Street, Colombo",
        PickupLat = 6.9271m,
        PickupLng = 79.8612m,
        DropoffAddress = "456 Dropoff Road, Kandy",
        DropoffLat = 7.2906m,
        DropoffLng = 80.6337m,
        PickupWindowStart = DateTimeOffset.UtcNow.AddDays(1),
        PickupWindowEnd = DateTimeOffset.UtcNow.AddDays(2)
    };

    // --- Create ---

    /// <summary>A load created without PostImmediately starts as Draft.</summary>
    [Fact]
    public async Task CreateAsync_CreatesLoadAsDraft_ByDefault()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var result = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        Assert.Equal("Draft", result.Status);
        Assert.Equal(shipperUserId, result.ShipperUserId);
        Assert.NotEqual(Guid.Empty, result.LoadId);
        Assert.False(string.IsNullOrWhiteSpace(result.ReferenceCode));
    }

    /// <summary>
    /// The generated reference code keeps its human-readable <c>LD-</c> prefix but carries a full
    /// GUID's worth of hex digits (32, not the old 9-digit truncated slice), and two separately
    /// created loads never collide.
    /// </summary>
    [Fact]
    public async Task CreateAsync_GeneratesAHighEntropyUniqueReferenceCode()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var first = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());
        var second = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        Assert.Matches("^LD-[0-9A-F]{32}$", first.ReferenceCode);
        Assert.NotEqual(first.ReferenceCode, second.ReferenceCode);
    }

    /// <summary>PostImmediately=true creates the load directly as Posted.</summary>
    [Fact]
    public async Task CreateAsync_CreatesLoadAsPosted_WhenPostImmediatelyTrue()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var result = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto(postImmediately: true));

        Assert.Equal("Posted", result.Status);
    }

    /// <summary>
    /// EstimatedPrice is computed automatically on create per ADR-015/ADR-019:
    /// baseFare + (distanceKm × ratePerKm) + (weightKg × ratePerKg), with ratePerKm derived from the
    /// seeded fuel price and vehicle-class efficiency (see <see cref="SeedDefaultPricingConfigAsync"/>).
    /// </summary>
    [Fact]
    public async Task CreateAsync_ComputesEstimatedPrice()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var request = ValidCreateLoadDto();

        var result = await sut.CreateAsync(shipperUserId, request);

        Assert.NotNull(result.EstimatedPrice);

        var distanceKm = HaversineDistanceKm(
            (double)request.PickupLat!.Value, (double)request.PickupLng!.Value,
            (double)request.DropoffLat!.Value, (double)request.DropoffLng!.Value);
        // 350m/15m are SeedDefaultPricingConfigAsync's PricePerLitre/FuelConsumptionLPer100Km;
        // 500m/10m/50m mirror PricingConstants.BaseFare/RatePerKg/DriverMaintenanceMarginAllowancePerKm.
        var ratePerKm = 350m / 100m * 15m + 50m;
        var expected = 500m + (decimal)distanceKm * ratePerKm + request.WeightKg * 10m;

        Assert.Equal(expected, result.EstimatedPrice!.Value, 2);
    }

    /// <summary>Create is blocked (not silently priced null) if no current fuel price is configured for any matching tier.</summary>
    [Fact]
    public async Task CreateAsync_ThrowsPricingConfigMissing_WhenNoFuelPriceConfigured()
    {
        using var dbContext = await CreateContextAsync(seedPricing: false);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var now = DateTimeOffset.UtcNow;
        dbContext.VehicleClassEfficiencies.Add(new VehicleClassEfficiency
        {
            VehicleClassEfficiencyId = Guid.NewGuid(),
            ClassLabel = VehicleClass.MiniTruck,
            MinPayloadKg = 0m,
            MaxPayloadKg = null,
            FuelConsumptionLPer100Km = 15m,
            Source = "test",
            EffectiveFrom = now,
            SetByUserId = shipperUserId,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(shipperUserId, ValidCreateLoadDto()));

        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
        Assert.Empty(dbContext.Loads);
    }

    /// <summary>Create is blocked (not silently priced null) if no vehicle-class tier covers the load's weight.</summary>
    [Fact]
    public async Task CreateAsync_ThrowsPricingConfigMissing_WhenNoMatchingTier()
    {
        using var dbContext = await CreateContextAsync(seedPricing: false);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var now = DateTimeOffset.UtcNow;
        dbContext.FuelPriceRates.Add(new FuelPriceRate
        {
            FuelPriceRateId = Guid.NewGuid(),
            FuelType = FuelType.AutoDiesel,
            PricePerLitre = 350m,
            Source = "test",
            EffectiveFrom = now,
            SetByUserId = shipperUserId,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(shipperUserId, ValidCreateLoadDto()));

        Assert.Equal(ErrorCode.PRICING_CONFIG_MISSING, exception.Code);
        Assert.Empty(dbContext.Loads);
    }

    /// <summary>Duplicates <c>LoadService.CalculateHaversineDistanceKm</c> for test-side expected-value computation.</summary>
    private static double HaversineDistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371.0;
        var lat1Rad = lat1 * Math.PI / 180.0;
        var lat2Rad = lat2 * Math.PI / 180.0;
        var deltaLatRad = (lat2 - lat1) * Math.PI / 180.0;
        var deltaLngRad = (lng2 - lng1) * Math.PI / 180.0;

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    /// <summary>A pickup window where End is not after Start is rejected before any DB write.</summary>
    [Fact]
    public async Task CreateAsync_Throws_ForInvalidPickupWindow()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var request = ValidCreateLoadDto();
        request.PickupWindowEnd = request.PickupWindowStart;

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(shipperUserId, request));

        Assert.Equal(ErrorCode.INVALID_PICKUP_WINDOW, exception.Code);
        Assert.Empty(dbContext.Loads);
    }

    /// <summary>Identical pickup and dropoff coordinates are rejected before any DB write, mirroring <c>ck_load_distinct_points</c>.</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenPickupAndDropoffCoordinatesAreIdentical()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var request = ValidCreateLoadDto();
        request.DropoffLat = request.PickupLat;
        request.DropoffLng = request.PickupLng;

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(shipperUserId, request));

        Assert.Equal(ErrorCode.LOAD_PICKUP_DROPOFF_IDENTICAL, exception.Code);
        Assert.Empty(dbContext.Loads);
    }

    /// <summary>Creating a load records a single LoadStatusHistory row from null to the initial status.</summary>
    [Fact]
    public async Task CreateAsync_WritesInitialLoadStatusHistoryRow()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var result = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        var historyRow = await dbContext.LoadStatusHistories.SingleAsync(h => h.LoadId == result.LoadId);
        Assert.Null(historyRow.FromStatus);
        Assert.Equal(LoadStatus.Draft, historyRow.ToStatus);
        Assert.Equal(shipperUserId, historyRow.ChangedByUserId);
        Assert.Null(historyRow.Reason);
    }

    /// <summary>The created load's response is enriched with the owning Shipper's display name.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsShipperName()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var result = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        Assert.Equal("Jane Shipper", result.ShipperName);
    }

    // --- Get one ---

    /// <summary>Fetching an existing load returns its full detail.</summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsLoad_WhenExists()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var created = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        var result = await sut.GetByIdAsync(created.LoadId, shipperUserId, UserRole.Shipper);

        Assert.Equal(created.LoadId, result.LoadId);
        Assert.Equal(created.CargoDescription, result.CargoDescription);
        Assert.Equal(created.ReferenceCode, result.ReferenceCode);
        Assert.Equal("Jane Shipper", result.ShipperName);
        var historyRow = Assert.Single(result.StatusHistory);
        Assert.Null(historyRow.FromStatus);
        Assert.Equal("Draft", historyRow.ToStatus);
        Assert.Equal(shipperUserId, historyRow.ChangedByUserId);
    }

    /// <summary>
    /// Every recorded status transition is included, newest first — proving the single-load fetch
    /// surfaces the load's full audit trail, not just its most recent row.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsStatusHistory_NewestFirst()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var created = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());
        await sut.CancelAsync(created.LoadId, shipperUserId, new CancelLoadDto { Reason = "Shipper changed plans" });

        var result = await sut.GetByIdAsync(created.LoadId, shipperUserId, UserRole.Shipper);

        Assert.Equal(2, result.StatusHistory.Count);
        Assert.Equal("Cancelled", result.StatusHistory[0].ToStatus);
        Assert.Equal("Draft", result.StatusHistory[0].FromStatus);
        Assert.Equal("Shipper changed plans", result.StatusHistory[0].Reason);
        Assert.Null(result.StatusHistory[1].FromStatus);
        Assert.Equal("Draft", result.StatusHistory[1].ToStatus);
    }

    /// <summary>
    /// Create/Update/Cancel responses leave StatusHistory empty — only the single-load fetch populates
    /// the full audit trail (see <see cref="LoadResponseDto.StatusHistory"/>).
    /// </summary>
    [Fact]
    public async Task CreateAsync_ReturnsEmptyStatusHistory()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var result = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        Assert.Empty(result.StatusHistory);
    }

    /// <summary>Fetching a nonexistent load throws a 404-shaped ApiException.</summary>
    [Fact]
    public async Task GetByIdAsync_Throws_WhenNotFound()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin));

        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, exception.Code);
    }

    /// <summary>A Shipper who does not own the load is forbidden, even though it exists.</summary>
    [Fact]
    public async Task GetByIdAsync_Throws_ForNonOwner()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var ownerId = await SeedShipperUserAsync(dbContext);
        var otherShipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, ownerId, LoadStatus.Draft);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.GetByIdAsync(load.LoadId, otherShipperId, UserRole.Shipper));

        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, exception.Code);
    }

    /// <summary>An Admin can fetch any load, regardless of who owns it.</summary>
    [Fact]
    public async Task GetByIdAsync_Succeeds_ForAdmin_OnAnyLoad()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var ownerId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, ownerId, LoadStatus.Draft);

        var result = await sut.GetByIdAsync(load.LoadId, Guid.NewGuid(), UserRole.Admin);

        Assert.Equal(load.LoadId, result.LoadId);
    }

    // --- Get list ---

    /// <summary>The list endpoint splits results across pages according to Page/PageSize.</summary>
    [Fact]
    public async Task GetListAsync_PaginatesResults()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        for (var i = 0; i < 5; i++)
        {
            await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());
        }

        var page1 = await sut.GetListAsync(new LoadListQueryDto { Page = 1, PageSize = 2 }, shipperUserId, UserRole.Shipper);
        var page2 = await sut.GetListAsync(new LoadListQueryDto { Page = 2, PageSize = 2 }, shipperUserId, UserRole.Shipper);

        Assert.Equal(5, page1.TotalItems);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        var page1Ids = page1.Items.Select(i => i.LoadId).ToHashSet();
        Assert.DoesNotContain(page2.Items, item => page1Ids.Contains(item.LoadId));
    }

    /// <summary>
    /// A page number large enough that <c>(page - 1) * pageSize</c> would overflow plain 32-bit
    /// arithmetic is rejected with a 400 instead of surfacing as an unhandled overflow/negative-Skip
    /// error.
    /// </summary>
    [Fact]
    public async Task GetListAsync_Throws_WhenPageOffsetOverflows()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.GetListAsync(new LoadListQueryDto { Page = int.MaxValue, PageSize = 100 }, shipperUserId, UserRole.Shipper));

        Assert.Equal(ErrorCode.LOAD_PAGE_OUT_OF_RANGE, exception.Code);
    }

    /// <summary>The Status filter returns only loads in that exact status.</summary>
    [Fact]
    public async Task GetListAsync_FiltersByStatus()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());
        var posted = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto(postImmediately: true));

        var result = await sut.GetListAsync(new LoadListQueryDto { Status = LoadStatus.Posted }, shipperUserId, UserRole.Shipper);

        var item = Assert.Single(result.Items);
        Assert.Equal(posted.LoadId, item.LoadId);
    }

    /// <summary>The Search filter matches a substring of CargoDescription, case-insensitively.</summary>
    [Fact]
    public async Task GetListAsync_FiltersBySearchTerm()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var target = ValidCreateLoadDto();
        target.CargoDescription = "Refrigerated seafood shipment";
        var created = await sut.CreateAsync(shipperUserId, target);
        await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        var result = await sut.GetListAsync(new LoadListQueryDto { Search = "SEAFOOD" }, shipperUserId, UserRole.Shipper);

        var item = Assert.Single(result.Items);
        Assert.Equal(created.LoadId, item.LoadId);
    }

    /// <summary>CreatedFrom alone returns only loads created on or after the given instant (inclusive lower bound).</summary>
    [Fact]
    public async Task GetListAsync_CreatedFromOnly_ReturnsLoadsCreatedOnOrAfter()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var baseTime = DateTimeOffset.UtcNow;
        var older = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        older.CreatedAt = baseTime.AddDays(-2);
        var boundary = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        boundary.CreatedAt = baseTime;
        var newer = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        newer.CreatedAt = baseTime.AddDays(2);
        await dbContext.SaveChangesAsync();

        var result = await sut.GetListAsync(new LoadListQueryDto { CreatedFrom = baseTime }, shipperUserId, UserRole.Shipper);

        var resultIds = result.Items.Select(i => i.LoadId).ToHashSet();
        Assert.Equal(2, result.TotalItems);
        Assert.Contains(boundary.LoadId, resultIds);
        Assert.Contains(newer.LoadId, resultIds);
        Assert.DoesNotContain(older.LoadId, resultIds);
    }

    /// <summary>CreatedTo alone returns only loads created on or before the given instant (inclusive upper bound).</summary>
    [Fact]
    public async Task GetListAsync_CreatedToOnly_ReturnsLoadsCreatedOnOrBefore()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var baseTime = DateTimeOffset.UtcNow;
        var older = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        older.CreatedAt = baseTime.AddDays(-2);
        var boundary = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        boundary.CreatedAt = baseTime;
        var newer = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        newer.CreatedAt = baseTime.AddDays(2);
        await dbContext.SaveChangesAsync();

        var result = await sut.GetListAsync(new LoadListQueryDto { CreatedTo = baseTime }, shipperUserId, UserRole.Shipper);

        var resultIds = result.Items.Select(i => i.LoadId).ToHashSet();
        Assert.Equal(2, result.TotalItems);
        Assert.Contains(older.LoadId, resultIds);
        Assert.Contains(boundary.LoadId, resultIds);
        Assert.DoesNotContain(newer.LoadId, resultIds);
    }

    /// <summary>CreatedFrom and CreatedTo together (AND) return only loads within that inclusive window.</summary>
    [Fact]
    public async Task GetListAsync_CreatedFromAndCreatedToTogether_ReturnsLoadsWithinRange()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var baseTime = DateTimeOffset.UtcNow;
        var beforeRange = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        beforeRange.CreatedAt = baseTime.AddDays(-2);
        var inRange = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        inRange.CreatedAt = baseTime;
        var afterRange = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        afterRange.CreatedAt = baseTime.AddDays(2);
        await dbContext.SaveChangesAsync();

        var result = await sut.GetListAsync(
            new LoadListQueryDto { CreatedFrom = baseTime.AddDays(-1), CreatedTo = baseTime.AddDays(1) },
            shipperUserId,
            UserRole.Shipper);

        var item = Assert.Single(result.Items);
        Assert.Equal(inRange.LoadId, item.LoadId);
    }

    /// <summary>A date range that matches no loads returns an empty page, not an error.</summary>
    [Fact]
    public async Task GetListAsync_CreatedRangeExcludingAllLoads_ReturnsEmptyPage()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());
        var futureFrom = DateTimeOffset.UtcNow.AddYears(1);

        var result = await sut.GetListAsync(new LoadListQueryDto { CreatedFrom = futureFrom }, shipperUserId, UserRole.Shipper);

        Assert.Equal(0, result.TotalItems);
        Assert.Empty(result.Items);
    }

    /// <summary>
    /// When every load shares the same primary sort value (here, CreatedAt), the LoadId tiebreaker
    /// still yields a total, repeatable order — proving pagination can't duplicate or skip rows at a
    /// page boundary just because several loads tie on the requested sort field.
    /// </summary>
    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task GetListAsync_OrdersDeterministically_WhenPrimarySortValuesAreTied(string sortDir)
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var tiedCreatedAt = DateTimeOffset.UtcNow;
        var loadIds = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
            load.CreatedAt = tiedCreatedAt;
            await dbContext.SaveChangesAsync();
            loadIds.Add(load.LoadId);
        }

        var ascending = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        var expectedOrder = ascending ? loadIds.OrderBy(id => id).ToList() : loadIds.OrderByDescending(id => id).ToList();

        var page1 = await sut.GetListAsync(new LoadListQueryDto { SortBy = "createdAt", SortDir = sortDir, Page = 1, PageSize = 2 }, shipperUserId, UserRole.Shipper);
        var page2 = await sut.GetListAsync(new LoadListQueryDto { SortBy = "createdAt", SortDir = sortDir, Page = 2, PageSize = 2 }, shipperUserId, UserRole.Shipper);

        var actualOrder = page1.Items.Concat(page2.Items).Select(i => i.LoadId).ToList();
        Assert.Equal(expectedOrder, actualOrder);
    }

    /// <summary>A Shipper only ever sees their own loads, even if they request a different shipperUserId filter.</summary>
    [Fact]
    public async Task GetListAsync_ScopesToOwnLoads_ForShipper()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperAId = await SeedShipperUserAsync(dbContext);
        var shipperBId = await SeedShipperUserAsync(dbContext);
        await sut.CreateAsync(shipperAId, ValidCreateLoadDto());
        await sut.CreateAsync(shipperBId, ValidCreateLoadDto());

        var result = await sut.GetListAsync(new LoadListQueryDto { ShipperUserId = shipperBId }, shipperAId, UserRole.Shipper);

        var item = Assert.Single(result.Items);
        var reloaded = await dbContext.Loads.SingleAsync(l => l.LoadId == item.LoadId);
        Assert.Equal(shipperAId, reloaded.ShipperUserId);
    }

    /// <summary>An Admin sees loads across every shipper, with no forced scoping.</summary>
    [Fact]
    public async Task GetListAsync_ReturnsAllLoads_ForAdmin()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperAId = await SeedShipperUserAsync(dbContext);
        var shipperBId = await SeedShipperUserAsync(dbContext);
        await sut.CreateAsync(shipperAId, ValidCreateLoadDto());
        await sut.CreateAsync(shipperBId, ValidCreateLoadDto());

        var result = await sut.GetListAsync(new LoadListQueryDto(), Guid.NewGuid(), UserRole.Admin);

        Assert.Equal(2, result.TotalItems);
    }

    /// <summary>
    /// Each list row is enriched with its own owning Shipper's display name — the main scenario this
    /// exists for, since an Admin's list view spans loads from multiple different shippers.
    /// </summary>
    [Fact]
    public async Task GetListAsync_ItemsIncludeEachOwnersShipperName()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperAId = await SeedShipperUserAsync(dbContext);
        var shipperBId = await SeedShipperUserAsync(dbContext, fullName: "Bob Shipper");
        await sut.CreateAsync(shipperAId, ValidCreateLoadDto());
        await sut.CreateAsync(shipperBId, ValidCreateLoadDto());

        var result = await sut.GetListAsync(new LoadListQueryDto(), Guid.NewGuid(), UserRole.Admin);

        var namesByShipper = result.Items.ToDictionary(i => i.ShipperName);
        Assert.Contains("Jane Shipper", namesByShipper.Keys);
        Assert.Contains("Bob Shipper", namesByShipper.Keys);
    }

    /// <summary>
    /// Each list row also carries its owner's raw id, not just the resolved display name — the
    /// frontend's id-based fallback label (formatShipperName) needs it when ShipperName can't be
    /// resolved, the same way the single-load response already does.
    /// </summary>
    [Fact]
    public async Task GetListAsync_ItemsIncludeShipperUserId()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var created = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        var result = await sut.GetListAsync(new LoadListQueryDto(), shipperUserId, UserRole.Shipper);

        var item = Assert.Single(result.Items);
        Assert.Equal(created.LoadId, item.LoadId);
        Assert.Equal(shipperUserId, item.ShipperUserId);
    }

    // --- Edit ---

    /// <summary>A load in Draft or Posted can have its content edited; Status is unaffected.</summary>
    [Fact]
    public async Task UpdateAsync_Succeeds_WhenStatusIsDraftOrPosted()
    {
        foreach (var status in new[] { LoadStatus.Draft, LoadStatus.Posted })
        {
            using var dbContext = await CreateContextAsync();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var result = await sut.UpdateAsync(load.LoadId, shipperUserId, ValidUpdateLoadDto());

            Assert.Equal("Updated cargo description", result.CargoDescription);
            Assert.Equal(750m, result.WeightKg);
            Assert.Equal(status.ToString(), result.Status);
            Assert.Equal("Jane Shipper", result.ShipperName);
        }
    }

    /// <summary>Editing a load's weight recomputes EstimatedPrice rather than leaving the stale value from create.</summary>
    [Fact]
    public async Task UpdateAsync_RecomputesEstimatedPrice_WhenWeightChanges()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var created = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        var updateRequest = ValidUpdateLoadDto();
        updateRequest.WeightKg = created.WeightKg + 1000m;

        var result = await sut.UpdateAsync(created.LoadId, shipperUserId, updateRequest);

        Assert.NotNull(result.EstimatedPrice);
        Assert.NotEqual(created.EstimatedPrice, result.EstimatedPrice);
    }

    /// <summary>A load past Posted (Matched or later, including terminal states) cannot be edited.</summary>
    [Fact]
    public async Task UpdateAsync_Throws_WhenStatusIsMatchedOrLater()
    {
        var lockedStatuses = new[] { LoadStatus.Matched, LoadStatus.InTransit, LoadStatus.Delivered, LoadStatus.Closed, LoadStatus.Cancelled };
        foreach (var status in lockedStatuses)
        {
            using var dbContext = await CreateContextAsync();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateAsync(load.LoadId, shipperUserId, ValidUpdateLoadDto()));

            Assert.Equal(ErrorCode.INVALID_LOAD_STATUS_TRANSITION, exception.Code);
        }
    }

    /// <summary>Identical pickup and dropoff coordinates are rejected before any DB write, mirroring <c>ck_load_distinct_points</c>.</summary>
    [Fact]
    public async Task UpdateAsync_Throws_WhenPickupAndDropoffCoordinatesAreIdentical()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        var request = ValidUpdateLoadDto();
        request.DropoffLat = request.PickupLat;
        request.DropoffLng = request.PickupLng;

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateAsync(load.LoadId, shipperUserId, request));

        Assert.Equal(ErrorCode.LOAD_PICKUP_DROPOFF_IDENTICAL, exception.Code);
    }

    /// <summary>Editing a nonexistent load throws a 404-shaped ApiException.</summary>
    [Fact]
    public async Task UpdateAsync_Throws_WhenNotFound()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidUpdateLoadDto()));

        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, exception.Code);
    }

    /// <summary>A Shipper who does not own the load is forbidden from editing it.</summary>
    [Fact]
    public async Task UpdateAsync_Throws_ForNonOwner()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var ownerId = await SeedShipperUserAsync(dbContext);
        var otherShipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, ownerId, LoadStatus.Draft);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateAsync(load.LoadId, otherShipperId, ValidUpdateLoadDto()));

        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, exception.Code);
    }

    /// <summary>
    /// If another request commits a change to this load between when this call's context loaded it
    /// and when it saves, the xmin concurrency token (LoadConfiguration) catches the lost-update race
    /// and this call gets a 409 instead of silently overwriting the concurrent change.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Throws409_WhenLoadWasModifiedConcurrently()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var seedContext = await CreateContextAsync(databaseName);
        var shipperUserId = await SeedShipperUserAsync(seedContext);
        var load = await SeedLoadAsync(seedContext, shipperUserId, LoadStatus.Draft);

        using var dbContext = await CreateContextAsync(databaseName);
        var sut = CreateSut(dbContext);
        // Pre-load the row into this test's own context so its tracked original xmin value goes
        // stale the moment the "concurrent" write below commits — mirroring two requests racing on
        // the same row, without needing two real concurrent threads.
        _ = await dbContext.Loads.SingleAsync(l => l.LoadId == load.LoadId);

        using (var concurrentContext = await CreateContextAsync(databaseName))
        {
            var concurrentlyLoadedRow = await concurrentContext.Loads.SingleAsync(l => l.LoadId == load.LoadId);
            concurrentlyLoadedRow.CargoDescription = "Changed by a concurrent request";
            // The InMemory provider (unlike real Postgres) never auto-advances a shadow "xmin"
            // property on its own, so this stands in for the row-version bump a real UPDATE would
            // cause — without it, the concurrency token never actually changes and the race this
            // test targets could never be observed under InMemory.
            concurrentContext.Entry(concurrentlyLoadedRow).Property<uint>("xmin").CurrentValue = 12345u;
            await concurrentContext.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateAsync(load.LoadId, shipperUserId, ValidUpdateLoadDto()));

        Assert.Equal(ErrorCode.LOAD_CONCURRENCY_CONFLICT, exception.Code);
    }

    // --- Cancel ---

    /// <summary>A load in Draft, Posted, or Matched can be cancelled.</summary>
    [Fact]
    public async Task CancelAsync_Succeeds_FromValidStatuses()
    {
        var cancellableStatuses = new[] { LoadStatus.Draft, LoadStatus.Posted, LoadStatus.Matched };
        foreach (var status in cancellableStatuses)
        {
            using var dbContext = await CreateContextAsync();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var result = await sut.CancelAsync(load.LoadId, shipperUserId, new CancelLoadDto { Reason = "Shipper changed plans" });

            Assert.Equal("Cancelled", result.Status);
            Assert.Equal("Jane Shipper", result.ShipperName);
        }
    }

    /// <summary>A load already InTransit or in a terminal status cannot be cancelled through this method.</summary>
    [Fact]
    public async Task CancelAsync_Throws_FromInvalidStatuses()
    {
        var nonCancellableStatuses = new[] { LoadStatus.InTransit, LoadStatus.Delivered, LoadStatus.Closed, LoadStatus.Cancelled };
        foreach (var status in nonCancellableStatuses)
        {
            using var dbContext = await CreateContextAsync();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var exception = await Assert.ThrowsAsync<ApiException>(() =>
                sut.CancelAsync(load.LoadId, shipperUserId, new CancelLoadDto { Reason = "Shipper changed plans" }));

            Assert.Equal(ErrorCode.INVALID_LOAD_STATUS_TRANSITION, exception.Code);
        }
    }

    /// <summary>Cancelling without a reason is rejected before the DB's own cancel-reason CHECK would ever see it.</summary>
    [Fact]
    public async Task CancelAsync_Throws_WhenReasonMissing()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CancelAsync(load.LoadId, shipperUserId, new CancelLoadDto { Reason = null }));

        Assert.Equal(ErrorCode.LOAD_CANCEL_REASON_REQUIRED, exception.Code);
    }

    /// <summary>Cancelling a nonexistent load throws a 404-shaped ApiException.</summary>
    [Fact]
    public async Task CancelAsync_Throws_WhenNotFound()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CancelAsync(Guid.NewGuid(), Guid.NewGuid(), new CancelLoadDto { Reason = "N/A" }));

        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, exception.Code);
    }

    /// <summary>A Shipper who does not own the load is forbidden from cancelling it.</summary>
    [Fact]
    public async Task CancelAsync_Throws_ForNonOwner()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var ownerId = await SeedShipperUserAsync(dbContext);
        var otherShipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, ownerId, LoadStatus.Draft);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CancelAsync(load.LoadId, otherShipperId, new CancelLoadDto { Reason = "Not my load" }));

        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, exception.Code);
    }

    /// <summary>Cancelling records a LoadStatusHistory row capturing the prior status, reason, and actor.</summary>
    [Fact]
    public async Task CancelAsync_WritesLoadStatusHistoryRow()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Posted);

        await sut.CancelAsync(load.LoadId, shipperUserId, new CancelLoadDto { Reason = "No longer needed" });

        var historyRow = await dbContext.LoadStatusHistories.SingleAsync(h => h.LoadId == load.LoadId);
        Assert.Equal(LoadStatus.Posted, historyRow.FromStatus);
        Assert.Equal(LoadStatus.Cancelled, historyRow.ToStatus);
        Assert.Equal("No longer needed", historyRow.Reason);
        Assert.Equal(shipperUserId, historyRow.ChangedByUserId);
    }

    /// <summary>
    /// If another request commits a change to this load between when this call's context loaded it
    /// and when it saves, the xmin concurrency token (LoadConfiguration) catches the lost-update race
    /// and this call gets a 409 instead of silently cancelling over a since-changed row.
    /// </summary>
    [Fact]
    public async Task CancelAsync_Throws409_WhenLoadWasModifiedConcurrently()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var seedContext = await CreateContextAsync(databaseName);
        var shipperUserId = await SeedShipperUserAsync(seedContext);
        var load = await SeedLoadAsync(seedContext, shipperUserId, LoadStatus.Draft);

        using var dbContext = await CreateContextAsync(databaseName);
        var sut = CreateSut(dbContext);
        _ = await dbContext.Loads.SingleAsync(l => l.LoadId == load.LoadId);

        using (var concurrentContext = await CreateContextAsync(databaseName))
        {
            var concurrentlyLoadedRow = await concurrentContext.Loads.SingleAsync(l => l.LoadId == load.LoadId);
            concurrentlyLoadedRow.CargoDescription = "Changed by a concurrent request";
            // See UpdateAsync_Throws409_WhenLoadWasModifiedConcurrently for why this manual bump is
            // needed under the InMemory provider.
            concurrentContext.Entry(concurrentlyLoadedRow).Property<uint>("xmin").CurrentValue = 12345u;
            await concurrentContext.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CancelAsync(load.LoadId, shipperUserId, new CancelLoadDto { Reason = "Shipper changed plans" }));

        Assert.Equal(ErrorCode.LOAD_CONCURRENCY_CONFLICT, exception.Code);
    }
}
