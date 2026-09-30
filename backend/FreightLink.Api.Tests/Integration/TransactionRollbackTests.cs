using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Proves multi-entity atomicity of a single <c>SaveChangesAsync</c> call against a real Postgres
/// instance: EF Core wraps each <c>SaveChangesAsync</c> in one implicit transaction, so if any entity
/// in the change-set fails to persist (here, a <see cref="LoadStatusHistory"/> row with a foreign key
/// pointing at a non-existent user), NOTHING in that change-set — including the otherwise-valid
/// <see cref="Load"/> row added alongside it — should end up in the database. EF Core's InMemory
/// provider does not enforce foreign keys the same way, so this behavior can only be demonstrated
/// against a real database, hence <see cref="PostgresWebApplicationFactory"/>.
/// </summary>
[Collection(PostgresCollection.Name)]
public class TransactionRollbackTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public TransactionRollbackTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SaveChangesAsync_RollsBackEntireChangeSet_WhenOneEntityViolatesAForeignKey()
    {
        var now = DateTimeOffset.UtcNow;

        await using var setup = _factory.CreateDbContext();
        var shipper = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"rollback-{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-hash",
            FullName = "Rollback Test Shipper",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        setup.Users.Add(shipper);
        await setup.SaveChangesAsync();

        var loadId = Guid.NewGuid();
        var referenceCode = $"LD-{Guid.NewGuid():N}"[..20];

        await using (var db = _factory.CreateDbContext())
        {
            db.Loads.Add(new Load
            {
                LoadId = loadId,
                ShipperUserId = shipper.UserId,
                ReferenceCode = referenceCode,
                CargoDescription = "Rollback test cargo",
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
            });

            // Deliberately invalid: ChangedByUserId references a user that does not exist, which
            // violates the FK constraint configured in LoadStatusHistoryConfiguration.
            db.LoadStatusHistories.Add(new LoadStatusHistory
            {
                LoadStatusHistoryId = Guid.NewGuid(),
                LoadId = loadId,
                ChangedByUserId = Guid.NewGuid(),
                FromStatus = null,
                ToStatus = LoadStatus.Posted,
                ChangedAt = now
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // A fresh context (so no first-level cache masks the real DB state) confirms neither row
        // persisted — the otherwise-valid Load was rolled back along with the invalid history row.
        await using var verify = _factory.CreateDbContext();
        var persistedLoad = await verify.Loads.FirstOrDefaultAsync(l => l.LoadId == loadId);
        var persistedHistory = await verify.LoadStatusHistories.FirstOrDefaultAsync(h => h.LoadId == loadId);

        Assert.Null(persistedLoad);
        Assert.Null(persistedHistory);
    }
}
