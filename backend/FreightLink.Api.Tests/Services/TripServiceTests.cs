using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Trips;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Services;

public class TripServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static TripService CreateSut(AppDbContext dbContext) => new(dbContext);

    private static async Task<(Trip trip, Guid shipperUserId, Guid agencyStaffUserId, Guid driverUserId, Guid otherUserId)> SeedTripHierarchyAsync(
        AppDbContext db,
        TripStatus tripStatus = TripStatus.Assigned,
        LoadStatus loadStatus = LoadStatus.Matched)
    {
        var now = DateTimeOffset.UtcNow;

        var shipper = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            FullName = "Shipper User",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var agencyStaffUser = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.AgencyStaff,
            Email = $"agency-{Guid.NewGuid():N}@example.com",
            FullName = "Agency Staff User",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var driverUser = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Driver,
            Email = $"driver-{Guid.NewGuid():N}@example.com",
            FullName = "Driver User",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var otherUser = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"other-{Guid.NewGuid():N}@example.com",
            FullName = "Other Shipper",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.AddRange(shipper, agencyStaffUser, driverUser, otherUser);

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = "Speedy Logistics",
            BusinessRegNo = $"REG-{Guid.NewGuid():N}",
            YardAddress = "123 Yard Road",
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Agencies.Add(agency);

        var agencyStaff = new AgencyStaff
        {
            UserId = agencyStaffUser.UserId,
            AgencyId = agency.AgencyId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AgencyStaff.Add(agencyStaff);

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            RegistrationNo = "WP-CAB-1234",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000,
            VolumeM3 = 25,
            Status = VehicleStatus.Available,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Vehicles.Add(vehicle);

        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            UserId = driverUser.UserId,
            AgencyId = agency.AgencyId,
            LicenceNo = "LIC12345",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Drivers.Add(driver);

        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipper.UserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Electronics",
            WeightKg = 1200,
            VolumeM3 = 8,
            PickupAddress = "Colombo Port",
            PickupLat = 6.9400m,
            PickupLng = 79.8500m,
            DropoffAddress = "Kandy City",
            DropoffLat = 7.2906m,
            DropoffLng = 80.6337m,
            PickupWindowStart = now.AddDays(1),
            PickupWindowEnd = now.AddDays(2),
            Status = loadStatus,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Loads.Add(load);

        var run = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AttemptNo = 1,
            Status = WorkflowRunStatus.Completed,
            TriggeredByUserId = shipper.UserId,
            StartedAt = now,
            CompletedAt = now
        };
        db.AgentWorkflowRuns.Add(run);

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AgencyId = agency.AgencyId,
            WorkflowRunId = run.WorkflowRunId,
            ProposedPrice = 45000m,
            RoutedDistanceKm = 115m,
            ProposedEtaMinutes = 180,
            Status = AssignmentStatus.Accepted,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Assignments.Add(assignment);

        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            AssignmentId = assignment.AssignmentId,
            VehicleId = vehicle.VehicleId,
            DriverId = driver.DriverId,
            Status = tripStatus,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Trips.Add(trip);

        await db.SaveChangesAsync();

        return (trip, shipper.UserId, agencyStaffUser.UserId, driverUser.UserId, otherUser.UserId);
    }

    [Fact]
    public async Task GetListAsync_ReturnsTrips_ForAdmin()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, _, _, _) = await SeedTripHierarchyAsync(db);

        var result = await sut.GetListAsync(new TripListQueryDto(), Guid.NewGuid(), UserRole.Admin);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal(trip.TripId, result.Items[0].TripId);
        Assert.Equal("Speedy Logistics", result.Items[0].AgencyName);
        Assert.Equal("Driver User", result.Items[0].DriverName);
    }

    [Fact]
    public async Task GetListAsync_ScopesToAgency_ForAgencyStaff()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, staffUserId, _, _) = await SeedTripHierarchyAsync(db);

        var result = await sut.GetListAsync(new TripListQueryDto(), staffUserId, UserRole.AgencyStaff);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal(trip.TripId, result.Items[0].TripId);

        var emptyResult = await sut.GetListAsync(new TripListQueryDto(), Guid.NewGuid(), UserRole.AgencyStaff);
        Assert.Equal(0, emptyResult.TotalItems);
    }

    [Fact]
    public async Task GetListAsync_ScopesToDriver_ForDriver()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, _, driverUserId, _) = await SeedTripHierarchyAsync(db);

        var result = await sut.GetListAsync(new TripListQueryDto(), driverUserId, UserRole.Driver);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal(trip.TripId, result.Items[0].TripId);

        var emptyResult = await sut.GetListAsync(new TripListQueryDto(), Guid.NewGuid(), UserRole.Driver);
        Assert.Equal(0, emptyResult.TotalItems);
    }

    [Fact]
    public async Task GetListAsync_RejectsShipper_With403()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (_, shipperUserId, _, _, _) = await SeedTripHierarchyAsync(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.GetListAsync(new TripListQueryDto(), shipperUserId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.TRIP_ACCESS_DENIED, ex.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDetail_ForShipper_OwnLoad()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, shipperUserId, _, _, _) = await SeedTripHierarchyAsync(db);

        var result = await sut.GetByIdAsync(trip.TripId, shipperUserId, UserRole.Shipper);

        Assert.Equal(trip.TripId, result.TripId);
        Assert.Equal("Colombo Port", result.PickupAddress);
        Assert.Equal("Kandy City", result.DropoffAddress);
        Assert.Equal("WP-CAB-1234", result.VehicleRegistrationNo);
    }

    [Fact]
    public async Task GetByIdAsync_DeniesOtherShipper_With403()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, _, _, otherUserId) = await SeedTripHierarchyAsync(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.GetByIdAsync(trip.TripId, otherUserId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.TRIP_ACCESS_DENIED, ex.Code);
    }

    [Fact]
    public async Task GetByIdAsync_Returns404_WhenTripDoesNotExist()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal(ErrorCode.TRIP_NOT_FOUND, ex.Code);
    }

    [Fact]
    public async Task ChangeStatusAsync_RequiresPickupEvidence_ForPickedUpTransition()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, staffUserId, _, _) = await SeedTripHierarchyAsync(db, TripStatus.Assigned);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ChangeStatusAsync(trip.TripId, staffUserId, UserRole.AgencyStaff, new ChangeTripStatusDto
            {
                TargetStatus = TripStatus.PickedUp
            }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        Assert.Equal(ErrorCode.TRIP_EVIDENCE_REQUIRED, ex.Code);
    }

    [Fact]
    public async Task UploadEvidenceAsync_IsDriverOnly_AndAdvancesStatus()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, staffUserId, driverUserId, _) = await SeedTripHierarchyAsync(db, TripStatus.Assigned);

        // Agency Staff can no longer submit evidence at all — the assigned Driver captures both
        // Proof of Pickup and Proof of Delivery over the course of a trip.
        var roleEx = await Assert.ThrowsAsync<ApiException>(() =>
            sut.UploadEvidenceAsync(trip.TripId, staffUserId, UserRole.AgencyStaff, new UploadTripEvidenceDto
            {
                EvidenceType = EvidenceType.PickupProof,
                PublicId = "pickup-photo-1"
            }));
        Assert.Equal(HttpStatusCode.Forbidden, roleEx.StatusCode);
        Assert.Equal(ErrorCode.TRIP_ACCESS_DENIED, roleEx.Code);

        // The assigned Driver successfully uploads PickupProof
        var uploadResult = await sut.UploadEvidenceAsync(trip.TripId, driverUserId, UserRole.Driver, new UploadTripEvidenceDto
        {
            EvidenceType = EvidenceType.PickupProof,
            PublicId = "pickup-photo-1"
        });
        Assert.Equal("pickup-photo-1", uploadResult.StorageKey);

        // Duplicate pickup evidence rejected
        var dupEx = await Assert.ThrowsAsync<ApiException>(() =>
            sut.UploadEvidenceAsync(trip.TripId, driverUserId, UserRole.Driver, new UploadTripEvidenceDto
            {
                EvidenceType = EvidenceType.PickupProof,
                PublicId = "pickup-photo-2"
            }));
        Assert.Equal(HttpStatusCode.Conflict, dupEx.StatusCode);
        Assert.Equal(ErrorCode.TRIP_EVIDENCE_ALREADY_EXISTS, dupEx.Code);

        // Now status advance to PickedUp succeeds (still callable by AgencyStaff or Driver)
        var statusResult = await sut.ChangeStatusAsync(trip.TripId, driverUserId, UserRole.Driver, new ChangeTripStatusDto
        {
            TargetStatus = TripStatus.PickedUp,
            Notes = "Loaded at port"
        });
        Assert.Equal("PickedUp", statusResult.Status);
        Assert.Single(statusResult.Events);
        Assert.Equal("PickedUp", statusResult.Events[0].ToStatus);
    }

    [Fact]
    public async Task ChangeStatusAsync_SyncsLoadStatus_ToInTransit_WhenTripAdvancesToPickedUp()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, _, driverUserId, _) = await SeedTripHierarchyAsync(db, TripStatus.Assigned);

        await db.TripEvidences.AddAsync(new TripEvidence
        {
            TripEvidenceId = Guid.NewGuid(),
            TripId = trip.TripId,
            CapturedByUserId = driverUserId,
            EvidenceType = EvidenceType.PickupProof,
            StorageKey = "pickup-photo-1",
            CapturedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        await sut.ChangeStatusAsync(trip.TripId, driverUserId, UserRole.Driver, new ChangeTripStatusDto
        {
            TargetStatus = TripStatus.PickedUp
        });

        var load = await db.Loads.AsNoTracking().FirstAsync();
        Assert.Equal(LoadStatus.InTransit, load.Status);

        var history = await db.LoadStatusHistories.AsNoTracking().SingleAsync(h => h.LoadId == load.LoadId);
        Assert.Equal(LoadStatus.Matched, history.FromStatus);
        Assert.Equal(LoadStatus.InTransit, history.ToStatus);
    }

    [Fact]
    public async Task ChangeStatusAsync_SyncsLoadStatus_ToDelivered_WhenTripCompletes()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, _, driverUserId, _) = await SeedTripHierarchyAsync(db, TripStatus.InTransit);

        await db.TripEvidences.AddAsync(new TripEvidence
        {
            TripEvidenceId = Guid.NewGuid(),
            TripId = trip.TripId,
            CapturedByUserId = driverUserId,
            EvidenceType = EvidenceType.DeliveryProof,
            StorageKey = "delivery-photo-1",
            CapturedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        await sut.ChangeStatusAsync(trip.TripId, driverUserId, UserRole.Driver, new ChangeTripStatusDto
        {
            TargetStatus = TripStatus.Delivered
        });

        var load = await db.Loads.AsNoTracking().FirstAsync();
        Assert.Equal(LoadStatus.Delivered, load.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_RejectsInvalidStatusTransition()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, staffUserId, _, _) = await SeedTripHierarchyAsync(db, TripStatus.Assigned);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.ChangeStatusAsync(trip.TripId, staffUserId, UserRole.AgencyStaff, new ChangeTripStatusDto
            {
                TargetStatus = TripStatus.InTransit
            }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        Assert.Equal(ErrorCode.INVALID_TRIP_STATUS_TRANSITION, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_CreatesTripAndInitialEvent_WhenValid()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (existingTrip, _, staffUserId, _, _) = await SeedTripHierarchyAsync(db, TripStatus.Delivered);

        var agency = await db.Agencies.FirstAsync();
        var load = await db.Loads.FirstAsync();
        var run = await db.AgentWorkflowRuns.FirstAsync();

        var newAssignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AgencyId = agency.AgencyId,
            WorkflowRunId = run.WorkflowRunId,
            ProposedPrice = 50000m,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(newAssignment);

        var newVehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            RegistrationNo = "WP-CAB-9999",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 6000,
            VolumeM3 = 30,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Vehicles.Add(newVehicle);

        var newDriverUser = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Driver,
            Email = "driver2@example.com",
            FullName = "Second Driver",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(newDriverUser);

        var newDriver = new Driver
        {
            DriverId = Guid.NewGuid(),
            UserId = newDriverUser.UserId,
            AgencyId = agency.AgencyId,
            LicenceNo = "LIC99999",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Drivers.Add(newDriver);
        await db.SaveChangesAsync();

        var result = await sut.CreateAsync(new CreateTripDto
        {
            AssignmentId = newAssignment.AssignmentId,
            VehicleId = newVehicle.VehicleId,
            DriverId = newDriver.DriverId,
            Notes = "Dispatch trip test"
        }, staffUserId, UserRole.AgencyStaff);

        Assert.NotNull(result);
        Assert.Equal("Assigned", result.Status);
        Assert.Equal(newVehicle.VehicleId, result.VehicleId);
        Assert.Equal(newDriver.DriverId, result.DriverId);
        Assert.Single(result.Events);
        Assert.Equal("Assigned", result.Events[0].ToStatus);
        Assert.Equal("Dispatch trip test", result.Events[0].Notes);
    }

    [Fact]
    public async Task CreateAsync_ThrowsConflict_WhenTripAlreadyExistsForAssignment()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, staffUserId, _, _) = await SeedTripHierarchyAsync(db, TripStatus.Assigned);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateAsync(new CreateTripDto
            {
                AssignmentId = trip.AssignmentId,
                VehicleId = trip.VehicleId,
                DriverId = trip.DriverId
            }, staffUserId, UserRole.AgencyStaff));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal(ErrorCode.TRIP_ALREADY_EXISTS, ex.Code);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesVehicleAndDriver_WhenAssigned()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, staffUserId, _, _) = await SeedTripHierarchyAsync(db, TripStatus.Assigned);

        var agency = await db.Agencies.FirstAsync();
        var replacementVehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            RegistrationNo = "WP-REP-5555",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 7000,
            VolumeM3 = 35,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Vehicles.Add(replacementVehicle);
        await db.SaveChangesAsync();

        var result = await sut.UpdateAsync(trip.TripId, new UpdateTripDto
        {
            VehicleId = replacementVehicle.VehicleId,
            Notes = "Reassigned vehicle due to scheduling"
        }, staffUserId, UserRole.AgencyStaff);

        Assert.Equal(replacementVehicle.VehicleId, result.VehicleId);
        Assert.Equal("WP-REP-5555", result.VehicleRegistrationNo);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsUnprocessableEntity_WhenTripNotAssigned()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, staffUserId, _, _) = await SeedTripHierarchyAsync(db, TripStatus.InTransit);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.UpdateAsync(trip.TripId, new UpdateTripDto
            {
                VehicleId = Guid.NewGuid()
            }, staffUserId, UserRole.AgencyStaff));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        Assert.Equal(ErrorCode.TRIP_CANNOT_BE_MODIFIED, ex.Code);
    }

    [Fact]
    public async Task CancelAsync_CancelsTripAndLogsEvent_WhenValid()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, staffUserId, _, _) = await SeedTripHierarchyAsync(db, TripStatus.Assigned);

        var result = await sut.CancelAsync(trip.TripId, new CancelTripDto
        {
            Reason = "Shipper requested cancellation"
        }, staffUserId, UserRole.AgencyStaff);

        Assert.Equal("Cancelled", result.Status);
        Assert.Contains(result.Events, e => e.ToStatus == "Cancelled" && e.Notes == "Shipper requested cancellation");
    }

    [Fact]
    public async Task CancelAsync_ThrowsUnprocessableEntity_WhenTripAlreadyDelivered()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);
        var (trip, _, staffUserId, _, _) = await SeedTripHierarchyAsync(db, TripStatus.Delivered);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CancelAsync(trip.TripId, new CancelTripDto { Reason = "Too late" }, staffUserId, UserRole.AgencyStaff));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        Assert.Equal(ErrorCode.INVALID_TRIP_STATUS_TRANSITION, ex.Code);
    }
}
