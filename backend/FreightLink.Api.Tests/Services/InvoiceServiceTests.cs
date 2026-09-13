using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="InvoiceService"/> covering create, read, list, update, status transitions, and voiding.
/// </summary>
public class InvoiceServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static InvoiceService CreateSut(AppDbContext dbContext) => new(dbContext);

    private static async Task<(User Shipper, Agency Agency, User AgencyStaffUser, Driver Driver, Trip Trip)> SeedTripGraphAsync(AppDbContext db)
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

        var driverUser = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Driver,
            Email = $"driver-{Guid.NewGuid():N}@example.com",
            FullName = "Test Driver",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = "Speedy Logistics",
            BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..12],
            YardAddress = "123 Port Road, Colombo",
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var agencyStaff = new AgencyStaff
        {
            AgencyId = agency.AgencyId,
            UserId = staffUser.UserId,
            JobTitle = "Dispatcher",
            CreatedAt = now,
            UpdatedAt = now
        };

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            RegistrationNo = "WP-CAB-1234",
            VehicleType = VehicleType.Container,
            CapacityKg = 15000,
            VolumeM3 = 45,
            Status = VehicleStatus.Available,
            CreatedAt = now,
            UpdatedAt = now
        };

        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            UserId = driverUser.UserId,
            LicenceNo = "DL-987654",
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
            CargoDescription = "Electronics & machinery",
            WeightKg = 2500m,
            VolumeM3 = 10m,
            PickupAddress = "Warehouse A, Colombo",
            PickupLat = 6.9271m,
            PickupLng = 79.8612m,
            DropoffAddress = "Factory B, Kandy",
            DropoffLat = 7.2906m,
            DropoffLng = 80.6337m,
            PickupWindowStart = now.AddHours(2),
            PickupWindowEnd = now.AddHours(6),
            Status = LoadStatus.InTransit,
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
            ProposedPrice = 45000m,
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
            Status = TripStatus.InTransit,
            CreatedAt = now,
            UpdatedAt = now,
            Assignment = assignment,
            Vehicle = vehicle,
            Driver = driver
        };

        db.Users.AddRange(shipper, staffUser, driverUser);
        db.Agencies.Add(agency);
        db.AgencyStaff.Add(agencyStaff);
        db.Vehicles.Add(vehicle);
        db.Drivers.Add(driver);
        db.Loads.Add(load);
        db.AgentWorkflowRuns.Add(workflowRun);
        db.Assignments.Add(assignment);
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        return (shipper, agency, staffUser, driver, trip);
    }

    [Fact]
    public async Task CreateAsync_ValidDraftRequest_CreatesInvoiceSuccessfully()
    {
        using var db = CreateContext();
        var (shipper, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 45000m,
            Currency = "LKR",
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            IssueImmediately = false
        };

        var result = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, request);

        Assert.NotNull(result);
        Assert.Equal(trip.TripId, result.TripId);
        Assert.Equal(45000m, result.Amount);
        Assert.Equal("LKR", result.Currency);
        Assert.Equal(InvoiceStatus.Draft, result.Status);
        Assert.StartsWith("INV-", result.InvoiceNumber);

        var saved = await db.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == result.InvoiceId);
        Assert.NotNull(saved);
        Assert.Equal(InvoiceStatus.Draft, saved.Status);
    }

    [Fact]
    public async Task CreateAsync_IssueImmediately_CreatesInvoiceInIssuedStatus()
    {
        using var db = CreateContext();
        var (_, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 50000m,
            Currency = "LKR",
            IssueImmediately = true
        };

        var result = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, request);

        Assert.Equal(InvoiceStatus.Issued, result.Status);
    }

    [Fact]
    public async Task CreateAsync_NonPositiveAmount_ThrowsBadRequest()
    {
        using var db = CreateContext();
        var (shipper, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 0m,
            Currency = "LKR"
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(shipper.UserId, UserRole.Shipper, request));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(ErrorCode.INVALID_INVOICE_AMOUNT, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_TripNotFound_ThrowsNotFound()
    {
        using var db = CreateContext();
        var sut = CreateSut(db);

        var request = new CreateInvoiceDto
        {
            TripId = Guid.NewGuid(),
            Amount = 1000m,
            Currency = "LKR"
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(Guid.NewGuid(), UserRole.Admin, request));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal(ErrorCode.TRIP_NOT_FOUND, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_DuplicateInvoiceForTrip_ThrowsConflict()
    {
        using var db = CreateContext();
        var (shipper, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 30000m,
            Currency = "LKR"
        };

        await sut.CreateAsync(shipper.UserId, UserRole.Shipper, request);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(shipper.UserId, UserRole.Shipper, request));
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal(ErrorCode.INVOICE_ALREADY_EXISTS_FOR_TRIP, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_UnrelatedUser_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (_, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var unrelatedUserId = Guid.NewGuid();
        var request = new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 10000m,
            Currency = "LKR"
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.CreateAsync(unrelatedUserId, UserRole.Shipper, request));
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.INVOICE_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task GetByIdAsync_AdminOrOwner_ReturnsInvoice()
    {
        using var db = CreateContext();
        var (shipper, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 25000m,
            Currency = "LKR"
        });

        var byShipper = await sut.GetByIdAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper);
        Assert.Equal(created.InvoiceId, byShipper.InvoiceId);

        var byAdmin = await sut.GetByIdAsync(created.InvoiceId, Guid.NewGuid(), UserRole.Admin);
        Assert.Equal(created.InvoiceId, byAdmin.InvoiceId);
    }

    [Fact]
    public async Task GetListAsync_FiltersCorrectly()
    {
        using var db = CreateContext();
        var (shipper, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 60000m,
            Currency = "LKR"
        });

        var list = await sut.GetListAsync(new InvoiceListQueryDto { Page = 1, PageSize = 10 }, shipper.UserId, UserRole.Shipper);

        Assert.Equal(1, list.TotalItems);
        Assert.Single(list.Items);
        Assert.Equal(60000m, list.Items[0].Amount);
    }

    [Fact]
    public async Task UpdateAsync_DraftInvoice_UpdatesSuccessfully()
    {
        using var db = CreateContext();
        var (shipper, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 40000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var updated = await sut.UpdateAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper, new UpdateInvoiceDto
        {
            Amount = 48000m,
            Currency = "USD"
        });

        Assert.Equal(48000m, updated.Amount);
        Assert.Equal("USD", updated.Currency);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesStatus()
    {
        using var db = CreateContext();
        var (shipper, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 35000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var result = await sut.UpdateStatusAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper, new UpdateInvoiceStatusDto
        {
            Status = InvoiceStatus.Issued
        });

        Assert.Equal(InvoiceStatus.Issued, result.Status);
    }

    [Fact]
    public async Task VoidAsync_ValidDraftOrIssued_SetsStatusToVoid()
    {
        using var db = CreateContext();
        var (shipper, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 35000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var voided = await sut.VoidAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper);
        Assert.Equal(InvoiceStatus.Void, voided.Status);
    }

    [Fact]
    public async Task UpdateAsync_DriverRole_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, _, driver, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 20000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.UpdateAsync(created.InvoiceId, driver.UserId, UserRole.Driver, new UpdateInvoiceDto
            {
                Amount = 1m,
                Currency = "LKR"
            }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.INVOICE_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task UpdateStatusAsync_DriverRole_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, _, driver, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 20000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.UpdateStatusAsync(created.InvoiceId, driver.UserId, UserRole.Driver,
                new UpdateInvoiceStatusDto { Status = InvoiceStatus.Issued }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.INVOICE_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task VoidAsync_DriverRole_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, _, driver, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(shipper.UserId, UserRole.Shipper, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 20000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.VoidAsync(created.InvoiceId, driver.UserId, UserRole.Driver));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.INVOICE_NOT_OWNED, ex.Code);
    }
}
