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
    /// <summary>Creates a fresh, isolated InMemory-backed <see cref="AppDbContext"/> for one test.</summary>
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Builds a real <see cref="LoadService"/> wired to the given DB context.</summary>
    private static LoadService CreateSut(AppDbContext dbContext) => new(dbContext);

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
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var result = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        Assert.Equal("Draft", result.Status);
        Assert.Equal(shipperUserId, result.ShipperUserId);
        Assert.NotEqual(Guid.Empty, result.LoadId);
        Assert.False(string.IsNullOrWhiteSpace(result.ReferenceCode));
    }

    /// <summary>PostImmediately=true creates the load directly as Posted.</summary>
    [Fact]
    public async Task CreateAsync_CreatesLoadAsPosted_WhenPostImmediatelyTrue()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var result = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto(postImmediately: true));

        Assert.Equal("Posted", result.Status);
    }

    /// <summary>A pickup window where End is not after Start is rejected before any DB write.</summary>
    [Fact]
    public async Task CreateAsync_Throws_ForInvalidPickupWindow()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var request = ValidCreateLoadDto();
        request.PickupWindowEnd = request.PickupWindowStart;

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(shipperUserId, request));

        Assert.Equal(ErrorCode.INVALID_PICKUP_WINDOW, exception.Code);
        Assert.Empty(dbContext.Loads);
    }

    /// <summary>Creating a load records a single LoadStatusHistory row from null to the initial status.</summary>
    [Fact]
    public async Task CreateAsync_WritesInitialLoadStatusHistoryRow()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var result = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        var historyRow = await dbContext.LoadStatusHistories.SingleAsync(h => h.LoadId == result.LoadId);
        Assert.Null(historyRow.FromStatus);
        Assert.Equal(LoadStatus.Draft, historyRow.ToStatus);
        Assert.Equal(shipperUserId, historyRow.ChangedByUserId);
        Assert.Null(historyRow.Reason);
    }

    // --- Get one ---

    /// <summary>Fetching an existing load returns its full detail.</summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsLoad_WhenExists()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var created = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        var result = await sut.GetByIdAsync(created.LoadId);

        Assert.Equal(created.LoadId, result.LoadId);
        Assert.Equal(created.CargoDescription, result.CargoDescription);
        Assert.Equal(created.ReferenceCode, result.ReferenceCode);
    }

    /// <summary>Fetching a nonexistent load throws a 404-shaped ApiException.</summary>
    [Fact]
    public async Task GetByIdAsync_Throws_WhenNotFound()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.GetByIdAsync(Guid.NewGuid()));

        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, exception.Code);
    }

    // --- Get list ---

    /// <summary>The list endpoint splits results across pages according to Page/PageSize.</summary>
    [Fact]
    public async Task GetListAsync_PaginatesResults()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        for (var i = 0; i < 5; i++)
        {
            await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());
        }

        var page1 = await sut.GetListAsync(new LoadListQueryDto { Page = 1, PageSize = 2 });
        var page2 = await sut.GetListAsync(new LoadListQueryDto { Page = 2, PageSize = 2 });

        Assert.Equal(5, page1.TotalItems);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        var page1Ids = page1.Items.Select(i => i.LoadId).ToHashSet();
        Assert.DoesNotContain(page2.Items, item => page1Ids.Contains(item.LoadId));
    }

    /// <summary>The Status filter returns only loads in that exact status.</summary>
    [Fact]
    public async Task GetListAsync_FiltersByStatus()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());
        var posted = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto(postImmediately: true));

        var result = await sut.GetListAsync(new LoadListQueryDto { Status = LoadStatus.Posted });

        var item = Assert.Single(result.Items);
        Assert.Equal(posted.LoadId, item.LoadId);
    }

    /// <summary>The Search filter matches a substring of CargoDescription, case-insensitively.</summary>
    [Fact]
    public async Task GetListAsync_FiltersBySearchTerm()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var target = ValidCreateLoadDto();
        target.CargoDescription = "Refrigerated seafood shipment";
        var created = await sut.CreateAsync(shipperUserId, target);
        await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        var result = await sut.GetListAsync(new LoadListQueryDto { Search = "SEAFOOD" });

        var item = Assert.Single(result.Items);
        Assert.Equal(created.LoadId, item.LoadId);
    }

    // --- Edit ---

    /// <summary>A load in Draft or Posted can have its content edited; Status is unaffected.</summary>
    [Fact]
    public async Task UpdateAsync_Succeeds_WhenStatusIsDraftOrPosted()
    {
        foreach (var status in new[] { LoadStatus.Draft, LoadStatus.Posted })
        {
            using var dbContext = CreateContext();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var result = await sut.UpdateAsync(load.LoadId, ValidUpdateLoadDto());

            Assert.Equal("Updated cargo description", result.CargoDescription);
            Assert.Equal(750m, result.WeightKg);
            Assert.Equal(status.ToString(), result.Status);
        }
    }

    /// <summary>A load past Posted (Matched or later, including terminal states) cannot be edited.</summary>
    [Fact]
    public async Task UpdateAsync_Throws_WhenStatusIsMatchedOrLater()
    {
        var lockedStatuses = new[] { LoadStatus.Matched, LoadStatus.InTransit, LoadStatus.Delivered, LoadStatus.Closed, LoadStatus.Cancelled };
        foreach (var status in lockedStatuses)
        {
            using var dbContext = CreateContext();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateAsync(load.LoadId, ValidUpdateLoadDto()));

            Assert.Equal(ErrorCode.INVALID_LOAD_STATUS_TRANSITION, exception.Code);
        }
    }

    /// <summary>Editing a nonexistent load throws a 404-shaped ApiException.</summary>
    [Fact]
    public async Task UpdateAsync_Throws_WhenNotFound()
    {
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() => sut.UpdateAsync(Guid.NewGuid(), ValidUpdateLoadDto()));

        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, exception.Code);
    }

    // --- Cancel ---

    /// <summary>A load in Draft, Posted, or Matched can be cancelled.</summary>
    [Fact]
    public async Task CancelAsync_Succeeds_FromValidStatuses()
    {
        var cancellableStatuses = new[] { LoadStatus.Draft, LoadStatus.Posted, LoadStatus.Matched };
        foreach (var status in cancellableStatuses)
        {
            using var dbContext = CreateContext();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var result = await sut.CancelAsync(load.LoadId, shipperUserId, new CancelLoadDto { Reason = "Shipper changed plans" });

            Assert.Equal("Cancelled", result.Status);
        }
    }

    /// <summary>A load already InTransit or in a terminal status cannot be cancelled through this method.</summary>
    [Fact]
    public async Task CancelAsync_Throws_FromInvalidStatuses()
    {
        var nonCancellableStatuses = new[] { LoadStatus.InTransit, LoadStatus.Delivered, LoadStatus.Closed, LoadStatus.Cancelled };
        foreach (var status in nonCancellableStatuses)
        {
            using var dbContext = CreateContext();
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
        using var dbContext = CreateContext();
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
        using var dbContext = CreateContext();
        var sut = CreateSut(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CancelAsync(Guid.NewGuid(), Guid.NewGuid(), new CancelLoadDto { Reason = "N/A" }));

        Assert.Equal(ErrorCode.LOAD_NOT_FOUND, exception.Code);
    }

    /// <summary>Cancelling records a LoadStatusHistory row capturing the prior status, reason, and actor.</summary>
    [Fact]
    public async Task CancelAsync_WritesLoadStatusHistoryRow()
    {
        using var dbContext = CreateContext();
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
}
