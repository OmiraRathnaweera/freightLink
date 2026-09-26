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
/// Unit tests for <see cref="InvoiceService"/> verifying RBAC permissions, payment workflow,
/// manual invoice CRUD, line items, status transitions, voiding, and role-scoped queries.
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

    private static async Task<(User Shipper, Agency Agency, User StaffUser, Driver Driver, Trip Trip)> SeedTripGraphAsync(AppDbContext db)
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
            FullName = "Test Agent Staff",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = "Apex Logistics",
            BusinessRegNo = "BR-TEST-1234",
            YardAddress = "Peliyagoda Hub",
            YardLat = 6.96m,
            YardLng = 79.88m,
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            Staff = new List<AgencyStaff>()
        };

        var agencyStaff = new AgencyStaff
        {
            UserId = staffUser.UserId,
            AgencyId = agency.AgencyId,
            JobTitle = "Operations Dispatcher",
            User = staffUser,
            Agency = agency,
            CreatedAt = now,
            UpdatedAt = now
        };
        agency.Staff.Add(agencyStaff);

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            RegistrationNo = "WP-CAD-1234",
            VehicleType = VehicleType.Container,
            CapacityKg = 10000m,
            VolumeM3 = 35m,
            Status = VehicleStatus.Available,
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

        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            UserId = driverUser.UserId,
            LicenceNo = "DL-TEST-9999",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)),
            Status = DriverStatus.Active,
            User = driverUser,
            CreatedAt = now,
            UpdatedAt = now
        };

        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipper.UserId,
            ShipperUser = shipper,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..10],
            CargoDescription = "Industrial Machinery",
            WeightKg = 4500m,
            VolumeM3 = 12m,
            PickupAddress = "Kelaniya Logistics Hub",
            PickupLat = 6.95m,
            PickupLng = 79.92m,
            DropoffAddress = "Kandy Industrial Park",
            DropoffLat = 7.29m,
            DropoffLng = 80.63m,
            PickupWindowStart = now.AddHours(2),
            PickupWindowEnd = now.AddHours(6),
            Status = LoadStatus.Delivered,
            CreatedAt = now,
            UpdatedAt = now
        };

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = load.LoadId,
            TriggeredByUserId = staffUser.UserId,
            AttemptNo = 1,
            Objective = "Match load to fleet",
            Status = WorkflowRunStatus.Completed,
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            Load = load,
            AgencyId = agency.AgencyId,
            Agency = agency,
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
            Assignment = assignment,
            VehicleId = vehicle.VehicleId,
            Vehicle = vehicle,
            DriverId = driver.DriverId,
            Driver = driver,
            Status = TripStatus.Delivered,
            CreatedAt = now,
            UpdatedAt = now
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

    // =========================================================================
    // 1. RBAC — Create Invoice
    // =========================================================================

    [Fact]
    public async Task CreateAsync_Agent_CreatesDraftSuccessfully()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            RecipientRole = UserRole.Shipper,
            Amount = 45000m,
            Currency = "LKR",
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            IssueImmediately = false,
            LineItems = new List<InvoiceLineItemDto>
            {
                new() { Description = "Standard Freight Haul", Quantity = 1, UnitPrice = 40000m, TaxRate = 12.5m },
                new() { Description = "Handling surcharge", Quantity = 1, UnitPrice = 5000m, TaxRate = 0 }
            }
        };

        var result = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, request);

        Assert.NotNull(result);
        Assert.Equal(trip.TripId, result.TripId);
        Assert.Equal(InvoiceStatus.Draft, result.Status);
        Assert.StartsWith("INV-", result.InvoiceNumber);
        Assert.Equal(2, result.LineItems.Count);
    }

    [Fact]
    public async Task CreateAsync_Shipper_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 45000m,
            Currency = "LKR"
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateAsync(shipper.UserId, UserRole.Shipper, request));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_Admin_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (_, _, _, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 45000m,
            Currency = "LKR"
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateAsync(Guid.NewGuid(), UserRole.Admin, request));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_NonPositiveAmount_ThrowsBadRequest()
    {
        using var db = CreateContext();
        var (_, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var request = new CreateInvoiceDto
        {
            TripId = trip.TripId,
            Amount = 0m,
            Currency = "LKR"
        };

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, request));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(ErrorCode.INVALID_INVOICE_AMOUNT, ex.Code);
    }

    // =========================================================================
    // 2. RBAC — Edit / Update Invoice
    // =========================================================================

    [Fact]
    public async Task UpdateAsync_Agent_DraftInvoice_UpdatesSuccessfully()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 30000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var updated = await sut.UpdateAsync(created.InvoiceId, staffUser.UserId, UserRole.AgencyStaff, new UpdateInvoiceDto
        {
            Amount = 38000m,
            Notes = "Updated pricing terms"
        });

        Assert.Equal(38000m, updated.Amount);
        Assert.Equal("Updated pricing terms", updated.Notes);
    }

    [Fact]
    public async Task UpdateAsync_Shipper_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 30000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.UpdateAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper, new UpdateInvoiceDto
            {
                Amount = 25000m
            }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task UpdateAsync_Admin_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 30000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.UpdateAsync(created.InvoiceId, Guid.NewGuid(), UserRole.Admin, new UpdateInvoiceDto
            {
                Amount = 25000m
            }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task UpdateAsync_WhenStatusIsIssued_ThrowsUnprocessableEntity()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 30000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.UpdateAsync(created.InvoiceId, staffUser.UserId, UserRole.AgencyStaff, new UpdateInvoiceDto
            {
                Amount = 40000m
            }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        Assert.Equal(ErrorCode.INVALID_INVOICE_STATUS_TRANSITION, ex.Code);
    }

    // =========================================================================
    // 3. RBAC — Issue Invoice & Delivery Notification
    // =========================================================================

    [Fact]
    public async Task IssueAsync_Agent_TransitionsToIssued()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 30000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var issued = await sut.IssueAsync(created.InvoiceId, staffUser.UserId, UserRole.AgencyStaff);

        Assert.Equal(InvoiceStatus.Issued, issued.Status);
        Assert.NotNull(issued.IssuedAt);
    }

    [Fact]
    public async Task IssueAsync_Shipper_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 30000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.IssueAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task IssueAsync_Admin_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 30000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.IssueAsync(created.InvoiceId, Guid.NewGuid(), UserRole.Admin));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    // =========================================================================
    // 4. RBAC — Delete / Void Invoice
    // =========================================================================

    [Fact]
    public async Task VoidAsync_Agent_WithReason_VoidsSuccessfully()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 25000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var voided = await sut.VoidAsync(created.InvoiceId, staffUser.UserId, UserRole.AgencyStaff, "Client requested cancellation");

        Assert.Equal(InvoiceStatus.Void, voided.Status);
        Assert.Equal("Client requested cancellation", voided.AuditTrail.VoidReason);
    }

    [Fact]
    public async Task VoidAsync_Shipper_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 25000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.VoidAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper, "Cancel"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task VoidAsync_Admin_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 25000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.VoidAsync(created.InvoiceId, Guid.NewGuid(), UserRole.Admin, "Admin void"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    // =========================================================================
    // 5. RBAC & Workflow — Shipper Payment
    // =========================================================================

    [Fact]
    public async Task PayAsync_AssignedShipper_IssuedStatus_TransitionsToPaid()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 45000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var paid = await sut.PayAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper, new PayInvoiceDto
        {
            PaymentReference = "TXN-PAYHERE-123456",
            PaymentMethod = "PayHere"
        });

        Assert.Equal(InvoiceStatus.Paid, paid.Status);
        Assert.NotNull(paid.PaidAt);
        Assert.Equal("TXN-PAYHERE-123456", paid.PaymentReference);

        // Verify payment record in database
        var paymentRow = await db.Payments.FirstOrDefaultAsync(p => p.InvoiceId == created.InvoiceId);
        Assert.NotNull(paymentRow);
        Assert.Equal(PaymentStatus.Success, paymentRow.Status);
        Assert.Equal("TXN-PAYHERE-123456", paymentRow.GatewayRef);
    }

    [Fact]
    public async Task PayAsync_UnassignedShipper_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 45000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var otherShipperId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.PayAsync(created.InvoiceId, otherShipperId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task PayAsync_Agent_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 45000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.PayAsync(created.InvoiceId, staffUser.UserId, UserRole.AgencyStaff));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task PayAsync_Admin_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 45000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.PayAsync(created.InvoiceId, Guid.NewGuid(), UserRole.Admin));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task PayAsync_WhenDraft_ThrowsBadRequest()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 45000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.PayAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, ex.Code);
    }

    [Fact]
    public async Task PayAsync_WhenAlreadyPaid_ThrowsBadRequest()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 45000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        await sut.PayAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.PayAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(ErrorCode.INVOICE_ALREADY_PAID, ex.Code);
    }

    [Fact]
    public async Task VoidAsync_WhenPaid_ThrowsBadRequest()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 45000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        await sut.PayAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.VoidAsync(created.InvoiceId, staffUser.UserId, UserRole.AgencyStaff, "Cannot void"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
    }

    // =========================================================================
    // 6. RBAC — View Invoices (GET /:id and GET /)
    // =========================================================================

    [Fact]
    public async Task GetByIdAsync_Admin_CanViewAnyInvoice()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 25000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var adminView = await sut.GetByIdAsync(created.InvoiceId, Guid.NewGuid(), UserRole.Admin);
        Assert.NotNull(adminView);
        Assert.Equal(created.InvoiceId, adminView.InvoiceId);
    }

    [Fact]
    public async Task GetByIdAsync_Shipper_ViewingDraft_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 25000m,
            Currency = "LKR",
            IssueImmediately = false
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.GetByIdAsync(created.InvoiceId, shipper.UserId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task GetByIdAsync_Shipper_ViewingOtherShipperInvoice_ThrowsForbidden()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        var created = await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 25000m,
            Currency = "LKR",
            IssueImmediately = true
        });

        var otherShipperId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            sut.GetByIdAsync(created.InvoiceId, otherShipperId, UserRole.Shipper));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal(ErrorCode.FORBIDDEN, ex.Code);
    }

    [Fact]
    public async Task GetListAsync_Shipper_ExcludesDraftInvoices()
    {
        using var db = CreateContext();
        var (shipper, _, staffUser, _, trip) = await SeedTripGraphAsync(db);
        var sut = CreateSut(db);

        // Create 1 Draft and 1 Issued invoice
        await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            TripId = trip.TripId,
            RecipientId = shipper.UserId,
            Amount = 25000m,
            Currency = "LKR",
            IssueImmediately = false // Draft
        });

        await sut.CreateAsync(staffUser.UserId, UserRole.AgencyStaff, new CreateInvoiceDto
        {
            RecipientId = shipper.UserId,
            RecipientRole = UserRole.Shipper,
            Amount = 35000m,
            Currency = "LKR",
            IssueImmediately = true // Issued
        });

        var shipperList = await sut.GetListAsync(new InvoiceListQueryDto(), shipper.UserId, UserRole.Shipper);
        Assert.Single(shipperList.Items);
        Assert.Equal(InvoiceStatus.Issued, shipperList.Items[0].Status);

        var adminList = await sut.GetListAsync(new InvoiceListQueryDto(), Guid.NewGuid(), UserRole.Admin);
        Assert.Equal(2, adminList.TotalItems);
    }
}
