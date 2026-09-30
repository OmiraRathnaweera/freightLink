using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="LoadService"/> covering create/get/list/edit/cancel and status-transition
/// enforcement. Backed by EF Core's InMemory provider — no real Postgres needed. This is also this
/// project's full "integration test" coverage for <see cref="LoadService"/>, since no controller/HTTP
/// layer exists yet for a <c>WebApplicationFactory</c>-based test to exercise.
/// </summary>
public class LoadServiceTests
{
    /// <summary>Creates a fresh, isolated InMemory-backed <see cref="AppDbContext"/> for one test.</summary>
    private static Task<AppDbContext> CreateContextAsync() => CreateContextAsync(Guid.NewGuid().ToString());

    /// <summary>
    /// Creates an InMemory-backed <see cref="AppDbContext"/> against a caller-supplied database name,
    /// so concurrency tests can open a second, independent context onto the same underlying data.
    /// </summary>
    private static Task<AppDbContext> CreateContextAsync(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return Task.FromResult(new AppDbContext(options));
    }

    /// <summary>Builds a real <see cref="LoadService"/> wired to the given DB context.</summary>
    private static LoadService CreateSut(AppDbContext dbContext) => new(dbContext);

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
    /// EstimatedPrice is left null on create — pricing is the AI agent's responsibility, not
    /// LoadService's; Load creation never depends on any pricing config existing.
    /// </summary>
    [Fact]
    public async Task CreateAsync_LeavesEstimatedPriceNull()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);

        var result = await sut.CreateAsync(shipperUserId, ValidCreateLoadDto());

        Assert.Null(result.EstimatedPrice);
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

    // --- Get one ---

    /// <summary>Fetching an existing load returns its full detail, including its status-history audit trail.</summary>
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
    /// Seeds an Agency + AgencyStaff user + a Load with one Assignment from that agency in the given
    /// status, for exercising the AgencyStaff branch of GetListAsync ("My Agency Shipments").
    /// </summary>
    private static async Task<Guid> SeedAgencyStaffWithAssignedLoadAsync(AppDbContext dbContext, AssignmentStatus assignmentStatus)
    {
        var now = DateTimeOffset.UtcNow;
        var agencyId = Guid.NewGuid();
        dbContext.Agencies.Add(new Agency
        {
            AgencyId = agencyId,
            Name = "Test Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "1 Yard Road",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        var staffUserId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            UserId = staffUserId,
            FullName = "Agency Staffer",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.AgencyStaff.Add(new AgencyStaff { UserId = staffUserId, AgencyId = agencyId, CreatedAt = now, UpdatedAt = now });

        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Test Cargo",
            WeightKg = 3000,
            VolumeM3 = 12,
            PickupAddress = "Origin",
            DropoffAddress = "Destination",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffLat = 7.2m,
            DropoffLng = 80.6m,
            Status = LoadStatus.Matched,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AttemptNo = 1,
            Status = WorkflowRunStatus.Completed,
            TriggeredByUserId = shipperUserId,
            StartedAt = now,
            CompletedAt = now
        };
        dbContext.AgentWorkflowRuns.Add(workflowRun);

        dbContext.Assignments.Add(new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 45000m,
            Status = assignmentStatus,
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync();
        return staffUserId;
    }

    /// <summary>
    /// Regression test: a load whose Assignment to the caller's agency is still Proposed (an AI
    /// match recommendation or manual proposal the agency hasn't accepted/declined yet) must NOT
    /// appear in "My Agency Shipments" — otherwise an unactioned proposal looks like a confirmed
    /// shipment. It should only appear once that agency has actually Accepted it.
    /// </summary>
    [Fact]
    public async Task GetListAsync_ForAgencyStaff_ExcludesLoadsWithOnlyAProposedAssignment()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var staffUserId = await SeedAgencyStaffWithAssignedLoadAsync(dbContext, AssignmentStatus.Proposed);

        var result = await sut.GetListAsync(new LoadListQueryDto(), staffUserId, UserRole.AgencyStaff);

        Assert.Empty(result.Items);
    }

    /// <summary>Once the agency has Accepted the assignment, the load does appear in "My Agency Shipments".</summary>
    [Fact]
    public async Task GetListAsync_ForAgencyStaff_IncludesLoadsWithAnAcceptedAssignment()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var staffUserId = await SeedAgencyStaffWithAssignedLoadAsync(dbContext, AssignmentStatus.Accepted);

        var result = await sut.GetListAsync(new LoadListQueryDto(), staffUserId, UserRole.AgencyStaff);

        Assert.Single(result.Items);
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

    /// <summary>
    /// A previously-estimated price is invalidated the moment any input the internal estimator prices
    /// against changes — weight here — since there is no synchronous re-estimation wired into this
    /// edit path and a stale price would otherwise keep showing as if it still reflected the load.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ClearsEstimatedPrice_WhenWeightChanges()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        load.EstimatedPrice = 5000m;
        await dbContext.SaveChangesAsync();

        var updateRequest = ValidUpdateLoadDto();
        updateRequest.WeightKg = load.WeightKg + 1000m;

        var result = await sut.UpdateAsync(load.LoadId, shipperUserId, updateRequest);

        Assert.Null(result.EstimatedPrice);
    }

    /// <summary>An edit that changes only non-pricing fields (e.g. cargo description) leaves an existing EstimatedPrice untouched.</summary>
    [Fact]
    public async Task UpdateAsync_PreservesEstimatedPrice_WhenPricingInputsUnchanged()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);
        load.EstimatedPrice = 5000m;
        await dbContext.SaveChangesAsync();

        var updateRequest = ValidUpdateLoadDto();
        updateRequest.WeightKg = load.WeightKg;
        updateRequest.VolumeM3 = load.VolumeM3;
        updateRequest.PickupLat = load.PickupLat;
        updateRequest.PickupLng = load.PickupLng;
        updateRequest.DropoffLat = load.DropoffLat;
        updateRequest.DropoffLng = load.DropoffLng;
        updateRequest.CargoDescription = "Changed description only";

        var result = await sut.UpdateAsync(load.LoadId, shipperUserId, updateRequest);

        Assert.Equal(5000m, result.EstimatedPrice);
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

    // --- ChangeStatus: cancel ---

    /// <summary>A load in Draft or Posted can be cancelled.</summary>
    [Fact]
    public async Task ChangeStatusAsync_Cancel_Succeeds_FromValidStatuses()
    {
        var cancellableStatuses = new[] { LoadStatus.Draft, LoadStatus.Posted };
        foreach (var status in cancellableStatuses)
        {
            using var dbContext = await CreateContextAsync();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var result = await sut.ChangeStatusAsync(load.LoadId, shipperUserId, new ChangeLoadStatusDto { Status = LoadStatus.Cancelled, Reason = "Shipper changed plans" });

            Assert.Equal("Cancelled", result.Status);
            Assert.Equal("Jane Shipper", result.ShipperName);
        }
    }

    /// <summary>A load already Matched, InTransit, or in a terminal status cannot be cancelled through this method.</summary>
    [Fact]
    public async Task ChangeStatusAsync_Cancel_Throws_FromInvalidStatuses()
    {
        var nonCancellableStatuses = new[] { LoadStatus.Matched, LoadStatus.InTransit, LoadStatus.Delivered, LoadStatus.Closed, LoadStatus.Cancelled };
        foreach (var status in nonCancellableStatuses)
        {
            using var dbContext = await CreateContextAsync();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var exception = await Assert.ThrowsAsync<ApiException>(() =>
                sut.ChangeStatusAsync(load.LoadId, shipperUserId, new ChangeLoadStatusDto { Status = LoadStatus.Cancelled, Reason = "Shipper changed plans" }));

            Assert.Equal(ErrorCode.INVALID_LOAD_STATUS_TRANSITION, exception.Code);
        }
    }

    /// <summary>Cancelling without a reason is rejected before the DB's own cancel-reason CHECK would ever see it.</summary>
    [Fact]
    public async Task ChangeStatusAsync_Cancel_Throws_WhenReasonMissing()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ChangeStatusAsync(load.LoadId, shipperUserId, new ChangeLoadStatusDto { Status = LoadStatus.Cancelled, Reason = null }));

        Assert.Equal(ErrorCode.LOAD_CANCEL_REASON_REQUIRED, exception.Code);
    }

    // --- ChangeStatus: publish ---

    /// <summary>A Draft load can be published, transitioning it to Posted.</summary>
    [Fact]
    public async Task ChangeStatusAsync_Publish_Succeeds_FromDraft()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);

        var result = await sut.ChangeStatusAsync(load.LoadId, shipperUserId, new ChangeLoadStatusDto { Status = LoadStatus.Posted });

        Assert.Equal("Posted", result.Status);
        Assert.Equal("Jane Shipper", result.ShipperName);
    }

    /// <summary>A load that is not Draft (already Posted, or any later/terminal status) cannot be published.</summary>
    [Fact]
    public async Task ChangeStatusAsync_Publish_Throws_FromNonDraftStatuses()
    {
        var nonDraftStatuses = new[] { LoadStatus.Posted, LoadStatus.Matched, LoadStatus.InTransit, LoadStatus.Delivered, LoadStatus.Closed, LoadStatus.Cancelled };
        foreach (var status in nonDraftStatuses)
        {
            using var dbContext = await CreateContextAsync();
            var sut = CreateSut(dbContext);
            var shipperUserId = await SeedShipperUserAsync(dbContext);
            var load = await SeedLoadAsync(dbContext, shipperUserId, status);

            var exception = await Assert.ThrowsAsync<ApiException>(() =>
                sut.ChangeStatusAsync(load.LoadId, shipperUserId, new ChangeLoadStatusDto { Status = LoadStatus.Posted }));

            Assert.Equal(ErrorCode.INVALID_LOAD_STATUS_TRANSITION, exception.Code);
        }
    }

    /// <summary>Publishing records a LoadStatusHistory row capturing the prior status and actor, with no reason.</summary>
    [Fact]
    public async Task ChangeStatusAsync_Publish_WritesLoadStatusHistoryRow()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Draft);

        await sut.ChangeStatusAsync(load.LoadId, shipperUserId, new ChangeLoadStatusDto { Status = LoadStatus.Posted });

        var historyRow = await dbContext.LoadStatusHistories.SingleAsync(h => h.LoadId == load.LoadId);
        Assert.Equal(LoadStatus.Draft, historyRow.FromStatus);
        Assert.Equal(LoadStatus.Posted, historyRow.ToStatus);
        Assert.Null(historyRow.Reason);
        Assert.Equal(shipperUserId, historyRow.ChangedByUserId);
    }

    // --- ChangeStatus: shared guards ---

    /// <summary>A Shipper who does not own the load is forbidden from changing its status.</summary>
    [Fact]
    public async Task ChangeStatusAsync_Throws_ForNonOwner()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var ownerId = await SeedShipperUserAsync(dbContext);
        var otherShipperId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, ownerId, LoadStatus.Draft);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ChangeStatusAsync(load.LoadId, otherShipperId, new ChangeLoadStatusDto { Status = LoadStatus.Posted }));

        Assert.Equal(ErrorCode.LOAD_NOT_OWNED, exception.Code);
    }

    /// <summary>
    /// A target status other than Posted/Cancelled is rejected outright — Matched and beyond are
    /// reached only by internal processes, never by this Shipper-facing endpoint.
    /// </summary>
    [Fact]
    public async Task ChangeStatusAsync_Throws_WhenTargetStatusIsNotPublishOrCancel()
    {
        using var dbContext = await CreateContextAsync();
        var sut = CreateSut(dbContext);
        var shipperUserId = await SeedShipperUserAsync(dbContext);
        var load = await SeedLoadAsync(dbContext, shipperUserId, LoadStatus.Posted);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ChangeStatusAsync(load.LoadId, shipperUserId, new ChangeLoadStatusDto { Status = LoadStatus.Matched }));

        Assert.Equal(ErrorCode.INVALID_LOAD_STATUS_TRANSITION, exception.Code);
    }
}
