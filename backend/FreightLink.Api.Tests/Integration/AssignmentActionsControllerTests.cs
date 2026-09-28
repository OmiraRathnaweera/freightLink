using System.Net;
using System.Net.Http.Json;
using FreightLink.Api.Common.Security;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Covers the public, token-gated email Accept/Decline action endpoint
/// (POST /api/v1/assignment-actions/respond) and its fail-closed guarantees:
/// a token is single-use, its sibling is invalidated together with it, and an assignment
/// already actioned another way can never be double-applied.
/// </summary>
public class AssignmentActionsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AssignmentActionsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private sealed record Scenario(Guid AssignmentId, Guid StaffUserId, string AcceptToken, string DeclineToken);

    /// <summary>Seeds a full Proposed assignment (agency + staff + shipper + load + an
    /// available vehicle/driver so AcceptAsync's auto-resolution succeeds) plus a fresh,
    /// still-active Accept/Decline action-token pair for it.</summary>
    private static async Task<Scenario> SeedProposedAssignmentWithTokensAsync(AppDbContext db)
    {
        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.Agencies.Add(new Agency
        {
            AgencyId = agencyId,
            Name = "Email Action Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "10 Yard Way",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        var staffUser = new User
        {
            UserId = staffUserId,
            FullName = "Email Action Staff",
            Email = $"action-staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Users.Add(staffUser);
        db.AgencyStaff.Add(new AgencyStaff { AgencyId = agencyId, UserId = staffUserId, CreatedAt = now, UpdatedAt = now });

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Email Action Shipper",
            Email = $"action-shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Users.Add(shipperUser);

        db.Loads.Add(new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-ACT-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Action Link Cargo",
            WeightKg = 2500,
            VolumeM3 = 10,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.1m,
            DropoffLng = 80.1m,
            // Matched, not Posted: by the time a Proposed assignment's proposal email exists
            // at all, ConfirmMatchAsync has always already moved the Load to Matched - a
            // Posted seed here would mask the real Matched -> Matched double-transition bug
            // ApproveAsync had (ck_lsh_transition), since accepting would then be a genuine
            // Posted -> Matched transition instead of the no-op the real flow always hits.
            Status = LoadStatus.Matched,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.Vehicles.Add(new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agencyId,
            RegistrationNo = $"WP-ACT-{Guid.NewGuid():N}"[..8],
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000,
            VolumeM3 = 20,
            Status = VehicleStatus.Available,
            CreatedAt = now,
            UpdatedAt = now
        });

        var driverUserId = Guid.NewGuid();
        db.Users.Add(new User
        {
            UserId = driverUserId,
            FullName = "Action Link Driver",
            Email = $"action-driver-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Driver,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Drivers.Add(new Driver
        {
            DriverId = Guid.NewGuid(),
            AgencyId = agencyId,
            UserId = driverUserId,
            LicenceNo = $"B-{Guid.NewGuid():N}"[..8],
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.AgentWorkflowRuns.Add(new AgentWorkflowRun
        {
            WorkflowRunId = workflowRunId,
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match load to agency",
            Status = WorkflowRunStatus.AwaitingApproval,
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRunId,
            ProposedPrice = 35000m,
            Status = AssignmentStatus.Proposed,
            CreatedAt = now,
            UpdatedAt = now
        });

        var acceptRaw = AccountTokens.CreateToken();
        var declineRaw = AccountTokens.CreateToken();
        var expiresAt = now.AddDays(7);

        db.AssignmentActionTokens.AddRange(
            new AssignmentActionToken
            {
                AssignmentActionTokenId = Guid.NewGuid(),
                AssignmentId = assignmentId,
                Action = AssignmentActionType.Accept,
                TokenHash = AccountTokens.HashToken(acceptRaw),
                ActingUserId = staffUserId,
                ExpiresAt = expiresAt,
                CreatedAt = now
            },
            new AssignmentActionToken
            {
                AssignmentActionTokenId = Guid.NewGuid(),
                AssignmentId = assignmentId,
                Action = AssignmentActionType.Decline,
                TokenHash = AccountTokens.HashToken(declineRaw),
                ActingUserId = staffUserId,
                ExpiresAt = expiresAt,
                CreatedAt = now
            });

        await db.SaveChangesAsync();

        return new Scenario(assignmentId, staffUserId, acceptRaw, declineRaw);
    }

    [Fact]
    public async Task Respond_WithAcceptToken_AcceptsAssignment_AndInvalidatesDeclineSibling()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await SeedProposedAssignmentWithTokensAsync(db);

        var res = await _client.PostAsJsonAsync("/api/v1/assignment-actions/respond", new AssignmentActionRespondDto { Token = scenario.AcceptToken });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Accepted", body!.Status);

        using var checkScope = _factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await checkDb.Assignments.FirstAsync(a => a.AssignmentId == scenario.AssignmentId);
        Assert.Equal(AssignmentStatus.Accepted, assignment.Status);

        var tokens = await checkDb.AssignmentActionTokens.Where(t => t.AssignmentId == scenario.AssignmentId).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.NotNull(t.ConsumedAt));

        // Regression check: the Load was already Matched before this accept (the real
        // production sequence - ConfirmMatchAsync always sets it before the proposal email
        // exists at all). ApproveAsync must not attempt a Matched -> Matched "transition" -
        // no LoadStatusHistory row should be written for a no-op status change. The InMemory
        // test provider doesn't enforce ck_lsh_transition itself (only real Postgres does), so
        // this asserts the C# guard directly rather than relying on the DB to reject it.
        var historyCount = await checkDb.LoadStatusHistories.CountAsync(h => h.LoadId == assignment.LoadId);
        Assert.Equal(0, historyCount);
    }

    [Fact]
    public async Task Respond_WithDeclineToken_DeclinesAssignment_AndInvalidatesAcceptSibling()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await SeedProposedAssignmentWithTokensAsync(db);

        var res = await _client.PostAsJsonAsync("/api/v1/assignment-actions/respond", new AssignmentActionRespondDto { Token = scenario.DeclineToken });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Declined", body!.Status);

        using var checkScope = _factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = await checkDb.AssignmentActionTokens.Where(t => t.AssignmentId == scenario.AssignmentId).ToListAsync();
        Assert.All(tokens, t => Assert.NotNull(t.ConsumedAt));
    }

    [Fact]
    public async Task Respond_ReusingAConsumedToken_FailsClosed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await SeedProposedAssignmentWithTokensAsync(db);

        var first = await _client.PostAsJsonAsync("/api/v1/assignment-actions/respond", new AssignmentActionRespondDto { Token = scenario.AcceptToken });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/v1/assignment-actions/respond", new AssignmentActionRespondDto { Token = scenario.AcceptToken });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Respond_AfterAssignmentAlreadyRespondedToAnotherWay_ReturnsConflict_AndNeverDoubleApplies()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await SeedProposedAssignmentWithTokensAsync(db);

        // Simulate the agency having already responded another way (e.g. the mobile app)
        // since the proposal email was sent.
        var assignment = await db.Assignments.FirstAsync(a => a.AssignmentId == scenario.AssignmentId);
        assignment.Status = AssignmentStatus.Declined;
        await db.SaveChangesAsync();

        var res = await _client.PostAsJsonAsync("/api/v1/assignment-actions/respond", new AssignmentActionRespondDto { Token = scenario.AcceptToken });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        using var checkScope = _factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        // The token must be burned even though it was refused - never left usable for a retry.
        var tokens = await checkDb.AssignmentActionTokens.Where(t => t.AssignmentId == scenario.AssignmentId).ToListAsync();
        Assert.All(tokens, t => Assert.NotNull(t.ConsumedAt));
        var unchangedAssignment = await checkDb.Assignments.FirstAsync(a => a.AssignmentId == scenario.AssignmentId);
        Assert.Equal(AssignmentStatus.Declined, unchangedAssignment.Status);
    }

    [Fact]
    public async Task Respond_WithUnknownToken_ReturnsBadRequest()
    {
        var res = await _client.PostAsJsonAsync("/api/v1/assignment-actions/respond", new AssignmentActionRespondDto { Token = "not-a-real-token" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Respond_WithExpiredToken_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await SeedProposedAssignmentWithTokensAsync(db);

        var expiredToken = await db.AssignmentActionTokens.FirstAsync(t => t.TokenHash == AccountTokens.HashToken(scenario.AcceptToken));
        expiredToken.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var res = await _client.PostAsJsonAsync("/api/v1/assignment-actions/respond", new AssignmentActionRespondDto { Token = scenario.AcceptToken });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
