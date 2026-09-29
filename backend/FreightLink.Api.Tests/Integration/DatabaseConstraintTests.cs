using FreightLink.Api.Data;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Proves that real Postgres-level unique constraints fire, using <see cref="PostgresWebApplicationFactory"/>
/// so each test hits an actual Postgres container rather than EF Core's InMemory provider (which does
/// not enforce relational constraints the same way). Every test bypasses the application-level
/// duplicate pre-checks entirely (e.g. <c>AuthService</c>'s <c>AnyAsync</c> lookup before insert) by
/// inserting rows directly via <see cref="AppDbContext.SaveChangesAsync"/>, so what's actually being
/// verified is the database schema's own constraint, not the app's defense-in-depth check.
/// </summary>
[Collection(PostgresCollection.Name)]
public class DatabaseConstraintTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public DatabaseConstraintTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static User NewUser(string email) => new()
    {
        UserId = Guid.NewGuid(),
        Role = UserRole.Shipper,
        Email = email,
        PasswordHash = "unused-hash",
        FullName = "Constraint Test User",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task SavingSecondUser_WithDuplicateEmail_ThrowsDbUpdateException()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        await using var db1 = _factory.CreateDbContext();
        db1.Users.Add(NewUser(email));
        await db1.SaveChangesAsync();

        await using var db2 = _factory.CreateDbContext();
        db2.Users.Add(NewUser(email));

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task SavingSecondShipperProfile_WithDuplicateBusinessRegNo_ThrowsDbUpdateException()
    {
        var regNo = $"REG-{Guid.NewGuid():N}"[..20];
        var now = DateTimeOffset.UtcNow;

        async Task<ShipperProfile> BuildProfileAsync(AppDbContext db)
        {
            var user = NewUser($"shipper-{Guid.NewGuid():N}@example.com");
            db.Users.Add(user);
            await db.SaveChangesAsync();

            return new ShipperProfile
            {
                UserId = user.UserId,
                CompanyName = "Constraint Test Shipper Co",
                BusinessRegNo = regNo,
                BillingAddress = "1 Test Road",
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        await using var db1 = _factory.CreateDbContext();
        db1.ShipperProfiles.Add(await BuildProfileAsync(db1));
        await db1.SaveChangesAsync();

        await using var db2 = _factory.CreateDbContext();
        db2.ShipperProfiles.Add(await BuildProfileAsync(db2));

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task SavingSecondLoad_WithDuplicateReferenceCode_ThrowsDbUpdateException()
    {
        var referenceCode = $"LD-{Guid.NewGuid():N}"[..20];
        var now = DateTimeOffset.UtcNow;

        async Task<Load> BuildLoadAsync(AppDbContext db)
        {
            var shipper = NewUser($"shipper-{Guid.NewGuid():N}@example.com");
            db.Users.Add(shipper);
            await db.SaveChangesAsync();

            return new Load
            {
                LoadId = Guid.NewGuid(),
                ShipperUserId = shipper.UserId,
                ReferenceCode = referenceCode,
                CargoDescription = "Test cargo",
                WeightKg = 100m,
                VolumeM3 = 1m,
                PickupAddress = "Pickup",
                PickupLat = 6.9m,
                PickupLng = 79.8m,
                DropoffAddress = "Dropoff",
                DropoffLat = 7.0m,
                DropoffLng = 80.0m,
                PickupWindowStart = now,
                PickupWindowEnd = now.AddHours(2),
                Status = LoadStatus.Posted,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        await using var db1 = _factory.CreateDbContext();
        db1.Loads.Add(await BuildLoadAsync(db1));
        await db1.SaveChangesAsync();

        await using var db2 = _factory.CreateDbContext();
        db2.Loads.Add(await BuildLoadAsync(db2));

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task SavingSecondRefreshToken_WithDuplicateTokenHash_ThrowsDbUpdateException()
    {
        var tokenHash = $"hash-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        async Task<RefreshToken> BuildTokenAsync(AppDbContext db)
        {
            var user = NewUser($"user-{Guid.NewGuid():N}@example.com");
            db.Users.Add(user);
            await db.SaveChangesAsync();

            return new RefreshToken
            {
                RefreshTokenId = Guid.NewGuid(),
                UserId = user.UserId,
                TokenHash = tokenHash,
                IssuedAt = now,
                ExpiresAt = now.AddDays(7)
            };
        }

        await using var db1 = _factory.CreateDbContext();
        db1.RefreshTokens.Add(await BuildTokenAsync(db1));
        await db1.SaveChangesAsync();

        await using var db2 = _factory.CreateDbContext();
        db2.RefreshTokens.Add(await BuildTokenAsync(db2));

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task SavingSecondAssignment_ForSameLoadAndAgency_ThrowsDbUpdateException()
    {
        var now = DateTimeOffset.UtcNow;

        await using var setup = _factory.CreateDbContext();

        var shipper = NewUser($"shipper-{Guid.NewGuid():N}@example.com");
        setup.Users.Add(shipper);

        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipper.UserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..20],
            CargoDescription = "Test cargo",
            WeightKg = 100m,
            VolumeM3 = 1m,
            PickupAddress = "Pickup",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Dropoff",
            DropoffLat = 7.0m,
            DropoffLng = 80.0m,
            PickupWindowStart = now,
            PickupWindowEnd = now.AddHours(2),
            Status = LoadStatus.Posted,
            CreatedAt = now,
            UpdatedAt = now
        };
        setup.Loads.Add(load);

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = "Constraint Test Agency",
            BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..20],
            YardAddress = "Yard",
            YardLat = 6.9m,
            YardLng = 79.9m,
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        setup.Agencies.Add(agency);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = load.LoadId,
            TriggeredByUserId = shipper.UserId,
            AttemptNo = 1,
            Objective = "Match this load",
            Status = WorkflowRunStatus.Running,
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        setup.AgentWorkflowRuns.Add(workflowRun);

        await setup.SaveChangesAsync();

        Assignment BuildAssignment() => new()
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AgencyId = agency.AgencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 1000m,
            Status = AssignmentStatus.Proposed,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var db1 = _factory.CreateDbContext();
        db1.Assignments.Add(BuildAssignment());
        await db1.SaveChangesAsync();

        await using var db2 = _factory.CreateDbContext();
        db2.Assignments.Add(BuildAssignment());

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }
}
