using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>
/// General-purpose service-layer tests for <see cref="AssignmentService"/>'s AcceptAsync/ApproveAsync/
/// DeclineAsync flows, complementing the two narrow scenario files (<c>Agent3MatchingPricingTests</c>,
/// <c>AssignmentServiceTriggerMatchTests</c>) which cover only matching/pricing and the fail-closed
/// agent-service-key check respectively. Backed by EF Core's InMemory provider, like <see cref="LoadServiceTests"/>.
/// </summary>
public class AssignmentServiceTests
{
    private class FakeEmailService : IEmailService
    {
        public Task SendAsync(FreightLink.Api.Common.Email.EmailMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task SendAgencyDeclinedAsync(string toEmail, string shipperName, string loadReference, string agencyName, int attemptNo, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task SendNewMatchFoundAsync(string toEmail, string shipperName, string loadReference, string agencyName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task SendNoAutomaticMatchFoundAsync(string toEmail, string shipperName, string loadReference, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AssignmentService CreateSut(AppDbContext db)
    {
        var pricingConfigService = new PricingConfigService(db);
        var pricingEstimator = new PricingEstimatorService(db, pricingConfigService);
        return new AssignmentService(db, new FakeEmailService(), pricingEstimator);
    }

    /// <summary>Seeds a full, ready-to-accept scenario: shipper, load (Matched), agency with one active
    /// vehicle/driver, agency-staff user, workflow run, and a Proposed assignment.</summary>
    private static async Task<(Assignment Assignment, Guid AgencyStaffUserId, Guid AgencyId)> SeedProposedAssignmentAsync(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;

        var shipper = new User
        {
            UserId = Guid.NewGuid(), Role = UserRole.Shipper, Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash", FullName = "Shipper", IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        db.Users.Add(shipper);

        var load = new Load
        {
            LoadId = Guid.NewGuid(), ShipperUserId = shipper.UserId, ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Cargo", WeightKg = 500m, VolumeM3 = 3m,
            PickupAddress = "A", PickupLat = 6.9m, PickupLng = 79.8m,
            DropoffAddress = "B", DropoffLat = 7.0m, DropoffLng = 80.0m,
            PickupWindowStart = now, PickupWindowEnd = now.AddHours(2),
            Status = LoadStatus.Matched, CreatedAt = now, UpdatedAt = now
        };
        db.Loads.Add(load);

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(), Name = "Test Agency", BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..15],
            YardAddress = "Yard", YardLat = 6.9m, YardLng = 79.9m, Status = AgencyStatus.Active,
            CreatedAt = now, UpdatedAt = now
        };
        db.Agencies.Add(agency);

        var staffUser = new User
        {
            UserId = Guid.NewGuid(), Role = UserRole.AgencyStaff, Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash", FullName = "Agency Staff", IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        db.Users.Add(staffUser);
        db.AgencyStaff.Add(new AgencyStaff { UserId = staffUser.UserId, AgencyId = agency.AgencyId, CreatedAt = now, UpdatedAt = now });

        db.Vehicles.Add(new Vehicle
        {
            VehicleId = Guid.NewGuid(), AgencyId = agency.AgencyId, RegistrationNo = "WP-TEST-001",
            VehicleType = VehicleType.Lorry, CapacityKg = 5000m, VolumeM3 = 20m,
            Status = VehicleStatus.Available, CreatedAt = now, UpdatedAt = now
        });

        var driverUser = new User
        {
            UserId = Guid.NewGuid(), Role = UserRole.Driver, Email = $"driver-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash", FullName = "Driver", IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        db.Users.Add(driverUser);
        db.Drivers.Add(new Driver
        {
            DriverId = Guid.NewGuid(), UserId = driverUser.UserId, AgencyId = agency.AgencyId,
            LicenceNo = "LIC-001", LicenceExpiry = DateOnly.FromDateTime(now.AddYears(2).Date),
            Status = DriverStatus.Active, CreatedAt = now, UpdatedAt = now
        });

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(), LoadId = load.LoadId, TriggeredByUserId = shipper.UserId,
            AttemptNo = 1, Objective = "Match load", Status = WorkflowRunStatus.AwaitingApproval,
            StartedAt = now, CreatedAt = now, UpdatedAt = now
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(), LoadId = load.LoadId, AgencyId = agency.AgencyId,
            WorkflowRunId = workflowRun.WorkflowRunId, ProposedPrice = 15000m,
            Status = AssignmentStatus.Proposed, CreatedAt = now, UpdatedAt = now
        };
        db.Assignments.Add(assignment);

        await db.SaveChangesAsync();
        return (assignment, staffUser.UserId, agency.AgencyId);
    }

    [Fact]
    public async Task AcceptAsync_OnProposedAssignment_TransitionsToAccepted_CreatesAssignedTrip_AndMarksLoadMatched()
    {
        var db = CreateContext();
        var (assignment, staffUserId, _) = await SeedProposedAssignmentAsync(db);
        var sut = CreateSut(db);

        var result = await sut.AcceptAsync(assignment.AssignmentId, null, staffUserId, UserRole.AgencyStaff);

        Assert.Equal(AssignmentStatus.Accepted.ToString(), result.Status);

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.AssignmentId == assignment.AssignmentId);
        Assert.NotNull(trip);
        Assert.Equal(TripStatus.Assigned, trip!.Status);

        var load = await db.Loads.FirstAsync(l => l.LoadId == assignment.LoadId);
        Assert.Equal(LoadStatus.Matched, load.Status);

        var approveDecision = await db.ApprovalDecisions.FirstOrDefaultAsync(
            d => d.WorkflowRunId == assignment.WorkflowRunId && d.Decision == ApprovalDecisionType.Approve);
        Assert.NotNull(approveDecision);
    }

    [Fact]
    public async Task AcceptAsync_ByStaffFromADifferentAgency_ThrowsForbidden()
    {
        var db = CreateContext();
        var (assignment, _, _) = await SeedProposedAssignmentAsync(db);
        var sut = CreateSut(db);

        var otherAgencyStaffId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var otherAgency = new Agency
        {
            AgencyId = Guid.NewGuid(), Name = "Other Agency", BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..15],
            YardAddress = "Yard2", YardLat = 6.8m, YardLng = 79.7m, Status = AgencyStatus.Active,
            CreatedAt = now, UpdatedAt = now
        };
        db.Agencies.Add(otherAgency);
        db.Users.Add(new User
        {
            UserId = otherAgencyStaffId, Role = UserRole.AgencyStaff, Email = $"other-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash", FullName = "Other Staff", IsActive = true, CreatedAt = now, UpdatedAt = now
        });
        db.AgencyStaff.Add(new AgencyStaff { UserId = otherAgencyStaffId, AgencyId = otherAgency.AgencyId, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.AcceptAsync(assignment.AssignmentId, null, otherAgencyStaffId, UserRole.AgencyStaff));

        Assert.Equal(ErrorCode.ASSIGNMENT_NOT_OWNED, ex.Code);
    }

    [Fact]
    public async Task DeclineAsync_OnProposedAssignment_TransitionsToDeclined_AndRevertsMatchedLoadToPosted()
    {
        var db = CreateContext();
        var (assignment, staffUserId, _) = await SeedProposedAssignmentAsync(db);
        var sut = CreateSut(db);

        var result = await sut.DeclineAsync(assignment.LoadId, new DeclineAssignmentDto { Reason = "No capacity" }, staffUserId, UserRole.AgencyStaff);

        Assert.Equal(AssignmentStatus.Declined.ToString(), result.Status);

        var load = await db.Loads.FirstAsync(l => l.LoadId == assignment.LoadId);
        Assert.Equal(LoadStatus.Posted, load.Status);

        var response = await db.AssignmentResponses.FirstOrDefaultAsync(r => r.AssignmentId == assignment.AssignmentId);
        Assert.NotNull(response);
        Assert.Equal(AssignmentResponseType.Declined, response!.Response);
        Assert.Equal("No capacity", response.DeclineReason);
    }

    [Fact]
    public async Task DeclineAsync_OnAlreadyAcceptedAssignment_ThrowsConflict()
    {
        var db = CreateContext();
        var (assignment, staffUserId, _) = await SeedProposedAssignmentAsync(db);
        var sut = CreateSut(db);

        await sut.AcceptAsync(assignment.AssignmentId, null, staffUserId, UserRole.AgencyStaff);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.DeclineAsync(assignment.LoadId, null, staffUserId, UserRole.AgencyStaff));

        Assert.Equal(ErrorCode.INVALID_TRIP_STATUS_TRANSITION, ex.Code);
    }

    [Fact]
    public async Task ApproveAsync_ByAdmin_BypassesAgencyOwnershipCheck()
    {
        var db = CreateContext();
        var (assignment, _, _) = await SeedProposedAssignmentAsync(db);
        var sut = CreateSut(db);

        var adminId = Guid.NewGuid();
        var result = await sut.ApproveAsync(assignment.AssignmentId, null, adminId, UserRole.Admin);

        Assert.Equal(AssignmentStatus.Accepted.ToString(), result.Status);
    }
}
