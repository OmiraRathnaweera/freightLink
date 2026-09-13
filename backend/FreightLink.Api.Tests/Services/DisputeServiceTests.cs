using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Disputes;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DisputeService"/> covering raising, reading, listing, editing, and resolving disputes.
/// </summary>
public class DisputeServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DisputeService CreateSut(AppDbContext dbContext) => new(dbContext);

    private static async Task<(User Shipper, Agency Agency, User StaffUser, Trip Trip)> SeedTripGraphAsync(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;

        var shipper = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            FullName = "Test Shipper",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var staffUser = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.AgencyStaff,
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            FullName = "Test Staff",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = "Prime Express",
            BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..12],
            YardAddress = "77 Harbour Road",
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var staff = new AgencyStaff
        {
            AgencyId = agency.AgencyId,
            UserId = staffUser.UserId,
            JobTitle = "Ops Lead",
            CreatedAt = now,
            UpdatedAt = now
        };

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            RegistrationNo = "WP-CAB-5555",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 8000,
            VolumeM3 = 25,
            Status = VehicleStatus.Available,
            CreatedAt = now,
            UpdatedAt = now
        };

        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            UserId = Guid.NewGuid(),
            LicenceNo = "DL-112233",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipper.UserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..10],
            CargoDescription = "Perishable food goods",
            WeightKg = 1200m,
            VolumeM3 = 5m,
            PickupAddress = "Colombo Fort",
            PickupLat = 6.9319m,
            PickupLng = 79.8478m,
            DropoffAddress = "Galle Port",
            DropoffLat = 6.0535m,
            DropoffLng = 80.2210m,
            PickupWindowStart = now.AddHours(1),
            PickupWindowEnd = now.AddHours(5),
            Status = LoadStatus.Delivered,
            CreatedAt = now,
            UpdatedAt = now
        };

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = load.LoadId,
            TriggeredByUserId = shipper.UserId,
            AttemptNo = 1,
            Objective = "Match load",
            Status = WorkflowRunStatus.Completed,
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AgencyId = agency.AgencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 28000m,
            Status = AssignmentStatus.Accepted,
            CreatedAt = now,
            UpdatedAt = now
        };

        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            AssignmentId = assignment.AssignmentId,
            VehicleId = vehicle.VehicleId,
            DriverId = driver.DriverId,
            Status = TripStatus.Delivered,
            CreatedAt = now,
            UpdatedAt = now,
            Assignment = assignment,
            Vehicle = vehicle,
            Driver = driver
        };

        db.Users.AddRange(shipper, staffUser);
        db.Agencies.Add(agency);
        db.AgencyStaff.Add(staff);
        db.Vehicles.Add(vehicle);
        db.Drivers.Add(driver);
        db.Loads.Add(load);
        db.AgentWorkflowRuns.Add(workflowRun);
        db.Assignments.Add(assignment);
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        return (shipper, agency, staffUser, trip);
    }

    [Fact]
    public async Task CreateAsync_ValidDispute_CreatesSuccessfully()
    {
        using var db = CreateContext();
        var (shipper, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Damage,
            Description = "Goods were damaged upon delivery at destination."
        };

        var result = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, request);

        Assert.NotNull(result);
        Assert.Equal(trip.TripId, result.TripId);
        Assert.Equal(shipper.UserId, result.RaisedByUserId);
        Assert.Equal(DisputeCategory.Damage, result.Category);
        Assert.Equal(DisputeStatus.Open, result.Status);
        Assert.Null(result.Resolution);
    }

    [Fact]
    public async Task CreateAsync_TripNotFound_ThrowsNotFound()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);

        var request = new CreateDisputeDto
        {
            TripId = Guid.NewGuid(),
            Category = DisputeCategory.Delay,
            Description = "Trip never started on time."
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(Guid.NewGuid(), UserRole.Shipper, request));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal(ErrorCode.TRIP_NOT_FOUND, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_UnrelatedUser_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (_, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Billing,
            Description = "Overcharged for transit."
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(Guid.NewGuid(), UserRole.Shipper, request));
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDisputeWithDetails()
    {
        using var db = CreateContext();
        var (shipper, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Damage,
            Description = "Box crushed in transit."
        });

        var fetched = await sut.GetByIdAsync(created.DisputeId, shipper.UserId, UserRole.Shipper);
        Assert.Equal(created.DisputeId, fetched.DisputeId);
        Assert.Equal("Box crushed in transit.", fetched.Description);
    }

    [Fact]
    public async Task UpdateAsync_OpenDisputeByRaiser_UpdatesSuccessfully()
    {
        using var db = CreateContext();
        var (shipper, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Damage,
            Description = "Initial description."
        });

        var updated = await sut.UpdateAsync(created.DisputeId, shipper.UserId, UserRole.Shipper, new UpdateDisputeDto
        {
            Category = DisputeCategory.Billing,
            Description = "Updated description with more details."
        });

        Assert.Equal(DisputeCategory.Billing, updated.Category);
        Assert.Equal("Updated description with more details.", updated.Description);
    }

    [Fact]
    public async Task ResolveAsync_AdminUser_ResolvesDisputeSuccessfully()
    {
        using var db = CreateContext();
        var (shipper, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Damage,
            Description = "Cargo received with broken seals."
        });

        var adminUserId = Guid.NewGuid();
        var resolved = await sut.ResolveAsync(created.DisputeId, adminUserId, UserRole.Admin, new ResolveDisputeDto
        {
            Outcome = DisputeOutcome.Upheld,
            Notes = "Damage confirmed by inspection logs; shipper credited."
        });

        Assert.Equal(DisputeStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.Resolution);
        Assert.Equal(adminUserId, resolved.Resolution.ResolvedByUserId);
        Assert.Equal(DisputeOutcome.Upheld, resolved.Resolution.Outcome);
        Assert.Equal("Damage confirmed by inspection logs; shipper credited.", resolved.Resolution.Notes);
    }

    [Fact]
    public async Task ResolveAsync_NonAdmin_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Delay,
            Description = "Arrived 4 hours late."
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ResolveAsync(created.DisputeId, shipper.UserId, UserRole.Shipper, new ResolveDisputeDto
        {
            Outcome = DisputeOutcome.Upheld
        }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_DuplicateLiveDisputeSameTripAndCategory_ThrowsConflict()
    {
        using var db = CreateContext();
        var (shipper, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        // First dispute — must succeed.
        await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Damage,
            Description = "Cargo damaged on first delivery attempt."
        });

        // Second dispute with the same (TripId, Category) while the first is still Open.
        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Damage,
            Description = "Cargo still damaged on second inspection."
        }));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal(ErrorCode.DISPUTE_ALREADY_EXISTS_FOR_TRIP_AND_CATEGORY, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_DifferentCategorySameTrip_Succeeds()
    {
        using var db = CreateContext();
        var (shipper, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        // One live Damage dispute.
        await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Damage,
            Description = "Cargo damaged on arrival at destination."
        });

        // Different category — must be allowed independently.
        var result = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateDisputeDto
        {
            TripId = trip.TripId,
            Category = DisputeCategory.Billing,
            Description = "Overcharged for the delivery service."
        });

        Assert.Equal(DisputeCategory.Billing, result.Category);
        Assert.Equal(DisputeStatus.Open, result.Status);
    }
}
