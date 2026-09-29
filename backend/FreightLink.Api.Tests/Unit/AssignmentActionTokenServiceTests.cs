using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Security;
using FreightLink.Api.Data;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AssignmentActionTokenService"/> — the single-use email Accept/Decline
/// link consumer. Wires a real <see cref="AssignmentService"/> underneath (rather than a hand-rolled
/// fake) since the whole point of this service is to delegate into it unchanged.
/// </summary>
public class AssignmentActionTokenServiceTests
{
    private class FakeEmailService : IEmailService
    {
        public Task SendAsync(FreightLink.Api.Common.Email.EmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAgencyDeclinedAsync(string toEmail, string shipperName, string loadReference, string agencyName, int attemptNo, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendNewMatchFoundAsync(string toEmail, string shipperName, string loadReference, string agencyName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendNoAutomaticMatchFoundAsync(string toEmail, string shipperName, string loadReference, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AssignmentActionTokenService CreateSut(AppDbContext db)
    {
        var pricingConfigService = new PricingConfigService(db);
        var pricingEstimator = new PricingEstimatorService(db, pricingConfigService);
        IAssignmentService assignmentService = new AssignmentService(db, new FakeEmailService(), pricingEstimator);
        return new AssignmentActionTokenService(db, assignmentService);
    }

    private static async Task<(Assignment Assignment, Guid StaffUserId, string AcceptRawToken, string DeclineRawToken)> SeedProposedAssignmentWithTokensAsync(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;

        var shipper = new User { UserId = Guid.NewGuid(), Role = UserRole.Shipper, Email = $"s-{Guid.NewGuid():N}@example.com", PasswordHash = "h", FullName = "Shipper", IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.Users.Add(shipper);

        var load = new Load
        {
            LoadId = Guid.NewGuid(), ShipperUserId = shipper.UserId, ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Cargo", WeightKg = 200m, VolumeM3 = 2m,
            PickupAddress = "A", PickupLat = 6.9m, PickupLng = 79.8m,
            DropoffAddress = "B", DropoffLat = 7.0m, DropoffLng = 80.0m,
            PickupWindowStart = now, PickupWindowEnd = now.AddHours(2),
            Status = LoadStatus.Matched, CreatedAt = now, UpdatedAt = now
        };
        db.Loads.Add(load);

        var agency = new Agency { AgencyId = Guid.NewGuid(), Name = "Agency", BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..15], YardAddress = "Y", YardLat = 6.9m, YardLng = 79.9m, Status = AgencyStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Agencies.Add(agency);

        var staffUser = new User { UserId = Guid.NewGuid(), Role = UserRole.AgencyStaff, Email = $"st-{Guid.NewGuid():N}@example.com", PasswordHash = "h", FullName = "Staff", IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.Users.Add(staffUser);
        db.AgencyStaff.Add(new AgencyStaff { UserId = staffUser.UserId, AgencyId = agency.AgencyId, CreatedAt = now, UpdatedAt = now });

        db.Vehicles.Add(new Vehicle { VehicleId = Guid.NewGuid(), AgencyId = agency.AgencyId, RegistrationNo = "WP-1", VehicleType = VehicleType.Lorry, CapacityKg = 5000m, VolumeM3 = 20m, Status = VehicleStatus.Available, CreatedAt = now, UpdatedAt = now });

        var driverUser = new User { UserId = Guid.NewGuid(), Role = UserRole.Driver, Email = $"dr-{Guid.NewGuid():N}@example.com", PasswordHash = "h", FullName = "Driver", IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.Users.Add(driverUser);
        db.Drivers.Add(new Driver { DriverId = Guid.NewGuid(), UserId = driverUser.UserId, AgencyId = agency.AgencyId, LicenceNo = "L1", LicenceExpiry = DateOnly.FromDateTime(now.AddYears(2).Date), Status = DriverStatus.Active, CreatedAt = now, UpdatedAt = now });

        var workflowRun = new AgentWorkflowRun { WorkflowRunId = Guid.NewGuid(), LoadId = load.LoadId, TriggeredByUserId = shipper.UserId, AttemptNo = 1, Objective = "Match", Status = WorkflowRunStatus.AwaitingApproval, StartedAt = now, CreatedAt = now, UpdatedAt = now };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment { AssignmentId = Guid.NewGuid(), LoadId = load.LoadId, AgencyId = agency.AgencyId, WorkflowRunId = workflowRun.WorkflowRunId, ProposedPrice = 12000m, Status = AssignmentStatus.Proposed, CreatedAt = now, UpdatedAt = now };
        db.Assignments.Add(assignment);

        var acceptRaw = Guid.NewGuid().ToString("N");
        var declineRaw = Guid.NewGuid().ToString("N");

        db.AssignmentActionTokens.Add(new AssignmentActionToken
        {
            AssignmentActionTokenId = Guid.NewGuid(), AssignmentId = assignment.AssignmentId, Action = AssignmentActionType.Accept,
            TokenHash = AccountTokens.HashToken(acceptRaw), ActingUserId = staffUser.UserId, ExpiresAt = now.AddDays(1), CreatedAt = now
        });
        db.AssignmentActionTokens.Add(new AssignmentActionToken
        {
            AssignmentActionTokenId = Guid.NewGuid(), AssignmentId = assignment.AssignmentId, Action = AssignmentActionType.Decline,
            TokenHash = AccountTokens.HashToken(declineRaw), ActingUserId = staffUser.UserId, ExpiresAt = now.AddDays(1), CreatedAt = now
        });

        await db.SaveChangesAsync();
        return (assignment, staffUser.UserId, acceptRaw, declineRaw);
    }

    [Fact]
    public async Task ConsumeActionTokenAsync_WithValidAcceptToken_AcceptsAssignment_AndConsumesBothTokens()
    {
        var db = CreateContext();
        var (assignment, _, acceptRaw, _) = await SeedProposedAssignmentWithTokensAsync(db);
        var sut = CreateSut(db);

        var result = await sut.ConsumeActionTokenAsync(acceptRaw);

        Assert.Equal(AssignmentStatus.Accepted.ToString(), result.Status);

        var tokens = await db.AssignmentActionTokens.Where(t => t.AssignmentId == assignment.AssignmentId).ToListAsync();
        Assert.All(tokens, t => Assert.NotNull(t.ConsumedAt));
    }

    [Fact]
    public async Task ConsumeActionTokenAsync_WithValidDeclineToken_DeclinesAssignment_AndConsumesBothTokens()
    {
        var db = CreateContext();
        var (assignment, _, _, declineRaw) = await SeedProposedAssignmentWithTokensAsync(db);
        var sut = CreateSut(db);

        var result = await sut.ConsumeActionTokenAsync(declineRaw);

        Assert.Equal(AssignmentStatus.Declined.ToString(), result.Status);

        var tokens = await db.AssignmentActionTokens.Where(t => t.AssignmentId == assignment.AssignmentId).ToListAsync();
        Assert.All(tokens, t => Assert.NotNull(t.ConsumedAt));
    }

    [Fact]
    public async Task ConsumeActionTokenAsync_WithUnknownToken_ThrowsInvalidOrExpired()
    {
        var db = CreateContext();
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ConsumeActionTokenAsync("does-not-exist"));

        Assert.Equal(ErrorCode.INVALID_OR_EXPIRED_ASSIGNMENT_TOKEN, ex.Code);
    }

    [Fact]
    public async Task ConsumeActionTokenAsync_WhenAcceptTokenAlreadyConsumed_ThrowsInvalidOrExpired()
    {
        var db = CreateContext();
        var (_, _, acceptRaw, _) = await SeedProposedAssignmentWithTokensAsync(db);
        var sut = CreateSut(db);

        await sut.ConsumeActionTokenAsync(acceptRaw);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ConsumeActionTokenAsync(acceptRaw));
        Assert.Equal(ErrorCode.INVALID_OR_EXPIRED_ASSIGNMENT_TOKEN, ex.Code);
    }

    [Fact]
    public async Task ConsumeActionTokenAsync_WhenDeclineTokenUsedAfterAcceptToken_ThrowsBecauseSiblingWasInvalidated()
    {
        var db = CreateContext();
        var (_, _, acceptRaw, declineRaw) = await SeedProposedAssignmentWithTokensAsync(db);
        var sut = CreateSut(db);

        await sut.ConsumeActionTokenAsync(acceptRaw);

        // The Decline token's sibling was invalidated the moment Accept was consumed, so it must
        // now fail closed with the same generic error rather than double-applying a decision.
        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ConsumeActionTokenAsync(declineRaw));
        Assert.Equal(ErrorCode.INVALID_OR_EXPIRED_ASSIGNMENT_TOKEN, ex.Code);
    }
}
