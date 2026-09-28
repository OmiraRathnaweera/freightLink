using FreightLink.Api.Data;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AnalyticsService"/>, backed directly by EF Core's InMemory provider with
/// hand-seeded rows — unlike the HTTP-pipeline <c>AdminAnalyticsControllerTests</c>, this bypasses
/// <c>Program.cs</c>'s own startup seeding entirely (no default agencies/users appear here), so every
/// count can be asserted exactly.
/// </summary>
public class AnalyticsServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User NewUser(UserRole role) => new()
    {
        UserId = Guid.NewGuid(),
        Role = role,
        Email = $"{role}-{Guid.NewGuid():N}@example.com",
        PasswordHash = "unused-hash",
        FullName = "Test User",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Load NewLoad(Guid shipperUserId, LoadStatus status) => new()
    {
        LoadId = Guid.NewGuid(),
        ShipperUserId = shipperUserId,
        ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
        CargoDescription = "Seeded cargo",
        WeightKg = 100m,
        VolumeM3 = 1m,
        PickupAddress = "Pickup",
        PickupLat = 6.9271m,
        PickupLng = 79.8612m,
        DropoffAddress = "Dropoff",
        DropoffLat = 7.2906m,
        DropoffLng = 80.6337m,
        PickupWindowStart = DateTimeOffset.UtcNow.AddDays(1),
        PickupWindowEnd = DateTimeOffset.UtcNow.AddDays(2),
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Agency NewAgency(AgencyStatus status) => new()
    {
        AgencyId = Guid.NewGuid(),
        Name = $"Agency {Guid.NewGuid():N}",
        BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..10],
        YardAddress = "Yard",
        YardLat = 6.9m,
        YardLng = 79.9m,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Invoice NewInvoice(InvoiceStatus status, decimal amount) => new()
    {
        InvoiceId = Guid.NewGuid(),
        InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..10],
        Amount = amount,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task GetSummaryAsync_ReturnsZeroTotals_WhenDatabaseIsEmpty()
    {
        using var dbContext = CreateContext();
        var sut = new AnalyticsService(dbContext);

        var result = await sut.GetSummaryAsync();

        Assert.Equal(0, result.Loads.Total);
        Assert.Empty(result.Loads.ByLabel);
        Assert.Equal(0, result.Agencies.Total);
        Assert.Equal(0, result.Trips.Total);
        Assert.Equal(0, result.Assignments.Total);
        Assert.Equal(0, result.Disputes.Total);
        Assert.Equal(0, result.Invoices.Counts.Total);
        Assert.Equal(0m, result.Invoices.TotalInvoicedAmount);
        Assert.Equal(0m, result.Invoices.TotalPaidAmount);
        Assert.Equal(0, result.UsersByRole.Total);
    }

    [Fact]
    public async Task GetSummaryAsync_GroupsLoadsByStatus_WithCorrectTotalAndBuckets()
    {
        using var dbContext = CreateContext();
        var shipper = NewUser(UserRole.Shipper);
        dbContext.Users.Add(shipper);
        dbContext.Loads.AddRange(
            NewLoad(shipper.UserId, LoadStatus.Draft),
            NewLoad(shipper.UserId, LoadStatus.Posted),
            NewLoad(shipper.UserId, LoadStatus.Posted),
            NewLoad(shipper.UserId, LoadStatus.Delivered));
        await dbContext.SaveChangesAsync();
        var sut = new AnalyticsService(dbContext);

        var result = await sut.GetSummaryAsync();

        Assert.Equal(4, result.Loads.Total);
        Assert.Equal(3, result.Loads.ByLabel.Count);
        Assert.Equal(2, result.Loads.ByLabel.Single(l => l.Label == "Posted").Count);
        Assert.Equal(1, result.Loads.ByLabel.Single(l => l.Label == "Draft").Count);
        Assert.Equal(1, result.Loads.ByLabel.Single(l => l.Label == "Delivered").Count);
    }

    [Fact]
    public async Task GetSummaryAsync_GroupsUsersByRole()
    {
        using var dbContext = CreateContext();
        dbContext.Users.AddRange(
            NewUser(UserRole.Shipper),
            NewUser(UserRole.Shipper),
            NewUser(UserRole.AgencyStaff),
            NewUser(UserRole.Driver),
            NewUser(UserRole.Admin));
        await dbContext.SaveChangesAsync();
        var sut = new AnalyticsService(dbContext);

        var result = await sut.GetSummaryAsync();

        Assert.Equal(5, result.UsersByRole.Total);
        Assert.Equal(2, result.UsersByRole.ByLabel.Single(l => l.Label == "Shipper").Count);
        Assert.Equal(1, result.UsersByRole.ByLabel.Single(l => l.Label == "AgencyStaff").Count);
        Assert.Equal(1, result.UsersByRole.ByLabel.Single(l => l.Label == "Driver").Count);
        Assert.Equal(1, result.UsersByRole.ByLabel.Single(l => l.Label == "Admin").Count);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesInvoiceTotals_ExcludingDraftAndVoid_FromTotalInvoiced()
    {
        using var dbContext = CreateContext();
        dbContext.Invoices.AddRange(
            NewInvoice(InvoiceStatus.Draft, 1000m),      // excluded from TotalInvoicedAmount
            NewInvoice(InvoiceStatus.Void, 2000m),        // excluded from TotalInvoicedAmount
            NewInvoice(InvoiceStatus.Issued, 3000m),      // invoiced, not paid
            NewInvoice(InvoiceStatus.Paid, 4000m),        // invoiced and paid
            NewInvoice(InvoiceStatus.Paid, 500m));        // invoiced and paid
        await dbContext.SaveChangesAsync();
        var sut = new AnalyticsService(dbContext);

        var result = await sut.GetSummaryAsync();

        Assert.Equal(5, result.Invoices.Counts.Total);
        Assert.Equal(3000m + 4000m + 500m, result.Invoices.TotalInvoicedAmount);
        Assert.Equal(4000m + 500m, result.Invoices.TotalPaidAmount);
        Assert.Equal(2, result.Invoices.Counts.ByLabel.Single(l => l.Label == "Paid").Count);
    }

    [Fact]
    public async Task GetSummaryAsync_GroupsAgenciesByStatus()
    {
        using var dbContext = CreateContext();
        dbContext.Agencies.AddRange(
            NewAgency(AgencyStatus.Pending),
            NewAgency(AgencyStatus.Active),
            NewAgency(AgencyStatus.Active),
            NewAgency(AgencyStatus.Suspended));
        await dbContext.SaveChangesAsync();
        var sut = new AnalyticsService(dbContext);

        var result = await sut.GetSummaryAsync();

        Assert.Equal(4, result.Agencies.Total);
        Assert.Equal(2, result.Agencies.ByLabel.Single(l => l.Label == "Active").Count);
        Assert.Equal(1, result.Agencies.ByLabel.Single(l => l.Label == "Pending").Count);
        Assert.Equal(1, result.Agencies.ByLabel.Single(l => l.Label == "Suspended").Count);
    }
}
