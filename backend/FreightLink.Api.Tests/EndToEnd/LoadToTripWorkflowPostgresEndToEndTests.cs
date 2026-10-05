using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using FreightLink.Api.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreightLink.Api.Tests.EndToEnd;

/// <summary>
/// Assignment 2 area E - the integrated business workflow on a REAL PostgreSQL database
/// (Testcontainers, full EF Core migration chain), not the InMemory provider used by
/// <see cref="LoadToTripWorkflowEndToEndTests"/>. Chain exercised over real HTTP:
/// Shipper registers/logs in -> posts a Load -> the Agentic AI service's four-step callback
/// sequence is replayed against the real internal endpoints -> human approval (Shipper confirms)
/// -> Agency accepts -> Trip created. The Python agent and LLM are the only simulated parts;
/// they are covered by the pytest suite under <c>agent/tests</c>.
/// Also proves the Shipper approval gate: only the owning Shipper can confirm, and only an agency the agent recommended.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LoadToTripWorkflowPostgresEndToEndTests : IClassFixture<PostgresWebApplicationFactory>
{
    private const string Password = "Sup3r$ecret1";

    private readonly PostgresWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LoadToTripWorkflowPostgresEndToEndTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private sealed record Scenario(
        Guid LoadId, Guid AgencyId, Guid WorkflowRunId, string ShipperToken, string AgencyToken);

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return request;
    }

    /// <summary>Runs steps 1-4: shipper + agency set-up, load posted, agent pipeline persisted. Stops before human approval.</summary>
    private async Task<Scenario> ArrangeThroughAgentPipelineAsync()
    {
        var shipperEmail = $"pg-e2e-shipper-{Guid.NewGuid():N}@example.com";
        (await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = shipperEmail, Password = Password, FullName = "PG E2E Shipper",
            CompanyName = "PG E2E Shipping", BillingAddress = "1 Test Lane, Colombo"
        })).EnsureSuccessStatusCode();
        var shipperLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = shipperEmail, Password = Password });
        shipperLogin.EnsureSuccessStatusCode();
        var shipperToken = (await shipperLogin.Content.ReadFromJsonAsync<TokenResponseDto>())!.AccessToken;

        Guid agencyId, shipperUserId;
        var agencyEmail = $"pg-e2e-agency-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var now = DateTimeOffset.UtcNow;
            shipperUserId = (await db.Users.FirstAsync(u => u.Email == shipperEmail)).UserId;

            var agency = new Agency
            {
                AgencyId = Guid.NewGuid(), Name = "PG E2E Agency", BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..15],
                YardAddress = "Yard", YardLat = 6.93m, YardLng = 79.85m, Status = AgencyStatus.Active,
                CreatedAt = now, UpdatedAt = now
            };
            db.Agencies.Add(agency);
            agencyId = agency.AgencyId;

            var staff = new User
            {
                UserId = Guid.NewGuid(), Role = UserRole.AgencyStaff, Email = agencyEmail,
                PasswordHash = hasher.Hash(Password), FullName = "PG E2E Staff",
                IsActive = true, EmailVerifiedAt = now, CreatedAt = now, UpdatedAt = now
            };
            db.Users.Add(staff);
            db.AgencyStaff.Add(new AgencyStaff { UserId = staff.UserId, AgencyId = agency.AgencyId, CreatedAt = now, UpdatedAt = now });

            db.Vehicles.Add(new Vehicle
            {
                VehicleId = Guid.NewGuid(), AgencyId = agency.AgencyId, RegistrationNo = $"WP-{Guid.NewGuid():N}"[..10],
                VehicleType = VehicleType.Lorry, CapacityKg = 5000m, VolumeM3 = 20m,
                Status = VehicleStatus.Available, CreatedAt = now, UpdatedAt = now
            });

            var driverUser = new User
            {
                UserId = Guid.NewGuid(), Role = UserRole.Driver, Email = $"pg-e2e-driver-{Guid.NewGuid():N}@example.com",
                PasswordHash = hasher.Hash("unused"), FullName = "PG E2E Driver", IsActive = true, CreatedAt = now, UpdatedAt = now
            };
            db.Users.Add(driverUser);
            db.Drivers.Add(new Driver
            {
                DriverId = Guid.NewGuid(), UserId = driverUser.UserId, AgencyId = agency.AgencyId,
                LicenceNo = $"LIC-{Guid.NewGuid():N}"[..12], LicenceExpiry = DateOnly.FromDateTime(now.AddYears(2).Date),
                Status = DriverStatus.Active, CreatedAt = now, UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        var agencyLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = agencyEmail, Password = Password });
        agencyLogin.EnsureSuccessStatusCode();
        var agencyToken = (await agencyLogin.Content.ReadFromJsonAsync<TokenResponseDto>())!.AccessToken;

        var createLoad = await _client.SendAsync(Authed(HttpMethod.Post, "/api/v1/loads", shipperToken, new CreateLoadDto
        {
            CargoDescription = "Postgres end-to-end cargo", WeightKg = 800m, VolumeM3 = 4m,
            PickupAddress = "123 Pickup Street, Colombo", PickupLat = 6.93m, PickupLng = 79.85m,
            DropoffAddress = "456 Dropoff Road, Kandy", DropoffLat = 7.29m, DropoffLng = 80.63m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(2), PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(6),
            PostImmediately = true
        }));
        createLoad.EnsureSuccessStatusCode();
        var load = (await createLoad.Content.ReadFromJsonAsync<LoadResponseDto>())!;
        Assert.Equal("Posted", load.Status);

        // Agent pipeline callbacks (the exact internal endpoints the Python service calls).
        async Task<HttpResponseMessage> InternalPostAsync(string url, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
            request.Headers.Add("X-Internal-Api-Key", PostgresWebApplicationFactory.ValidInternalApiKey);
            return await _client.SendAsync(request);
        }

        var runResponse = await InternalPostAsync("/internal/agent-workflow-runs",
            new CreateAgentWorkflowRunRequestDto { LoadId = load.LoadId, TriggeredByUserId = shipperUserId, AttemptNo = 1 });
        runResponse.EnsureSuccessStatusCode();
        var runId = (await runResponse.Content.ReadFromJsonAsync<CreateAgentWorkflowRunResponseDto>())!.WorkflowRunId;

        var t0 = DateTimeOffset.UtcNow;
        var steps = new (int No, AgentRole Role, string Json)[]
        {
            (1, AgentRole.Planner, "{\"objective\":\"Find an agency for this load\",\"steps\":[\"Planner\",\"DomainAnalysis\",\"MatchingPricing\",\"ValidationSafety\"]}"),
            (2, AgentRole.DomainAnalysis, $"{{\"candidates\":[{{\"agencyId\":\"{agencyId}\",\"rank\":1,\"eligible\":true,\"eligibilityScore\":100}}]}}"),
            (3, AgentRole.MatchingPricing, $"{{\"selectedAgencyId\":\"{agencyId}\",\"proposedPrice\":22000,\"cargoDistanceKm\":115.4,\"etaMinutes\":150,\"suggestedVehicleClass\":\"MiniTruck\"}}"),
            (4, AgentRole.ValidationSafety, "{\"recommendation\":\"Approve\"}"),
        };
        foreach (var (no, role, json) in steps)
        {
            var response = await InternalPostAsync($"/internal/agent-workflow-runs/{runId}/steps", new ReportAgentStepRequestDto
            {
                StepNo = no, AgentRole = role, Status = AgentStepStatus.Succeeded, OutputJson = json,
                StartedAt = t0.AddSeconds(no), CompletedAt = t0.AddSeconds(no + 1)
            });
            response.EnsureSuccessStatusCode();
        }

        return new Scenario(load.LoadId, agencyId, runId, shipperToken, agencyToken);
    }

    [Fact]
    public async Task FullWorkflow_ShipperPosts_AgentsRun_ShipperApproves_AgencyAccepts_TripPersistedInPostgres()
    {
        var s = await ArrangeThroughAgentPipelineAsync();

        var confirm = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/loads/{s.LoadId}/match/confirm", s.ShipperToken,
            new ConfirmMatchDto { AgencyId = s.AgencyId }));
        Assert.True(confirm.IsSuccessStatusCode, $"Confirm failed: {await confirm.Content.ReadAsStringAsync()}");

        var accept = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/assignments/{s.LoadId}/accept", s.AgencyToken));
        Assert.True(accept.IsSuccessStatusCode, $"Accept failed: {await accept.Content.ReadAsStringAsync()}");

        // Query PostgreSQL directly: every row must tell one consistent story.
        await using var db = _factory.CreateDbContext();

        Assert.Equal(LoadStatus.Matched, (await db.Loads.AsNoTracking().FirstAsync(l => l.LoadId == s.LoadId)).Status);

        var assignment = await db.Assignments.AsNoTracking().FirstAsync(a => a.LoadId == s.LoadId);
        Assert.Equal(AssignmentStatus.Accepted, assignment.Status);
        Assert.Equal(s.AgencyId, assignment.AgencyId);

        var trip = await db.Trips.AsNoTracking().SingleAsync(t => t.AssignmentId == assignment.AssignmentId);
        Assert.Equal(TripStatus.Assigned, trip.Status);

        var persistedSteps = await db.AgentSteps.AsNoTracking()
            .Where(x => x.WorkflowRunId == s.WorkflowRunId).OrderBy(x => x.StepNo).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3, 4 }, persistedSteps.Select(x => x.StepNo).ToArray());
        Assert.All(persistedSteps, x => Assert.Equal(AgentStepStatus.Succeeded, x.Status));
        Assert.Equal(
            new[] { AgentRole.Planner, AgentRole.DomainAnalysis, AgentRole.MatchingPricing, AgentRole.ValidationSafety },
            persistedSteps.Select(x => x.AgentRole).ToArray());
    }

    [Fact]
    public async Task ApprovalGate_ShipperCannotConfirmAnAgencyTheAgentNeverRecommended()
    {
        var s = await ArrangeThroughAgentPipelineAsync();

        var confirm = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/loads/{s.LoadId}/match/confirm", s.ShipperToken,
            new ConfirmMatchDto { AgencyId = Guid.NewGuid() }));

        Assert.False(confirm.IsSuccessStatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.False(await db.Assignments.AnyAsync(a => a.LoadId == s.LoadId && a.AgencyId != s.AgencyId));
    }

    [Fact]
    public async Task AcceptingTwice_NeverCreatesASecondTripOrAssignment()
    {
        var s = await ArrangeThroughAgentPipelineAsync();
        (await _client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/loads/{s.LoadId}/match/confirm", s.ShipperToken,
            new ConfirmMatchDto { AgencyId = s.AgencyId }))).EnsureSuccessStatusCode();
        (await _client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/assignments/{s.LoadId}/accept", s.AgencyToken))).EnsureSuccessStatusCode();

        // The service treats a repeat accept as idempotent (2xx) or a conflict (4xx); it must never be a 5xx
        // and must never create a second row.
        var second = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/assignments/{s.LoadId}/accept", s.AgencyToken));

        Assert.True((int)second.StatusCode < 500, $"Repeat accept returned {(int)second.StatusCode}");
        await using var db = _factory.CreateDbContext();
        Assert.Equal(1, await db.Trips.CountAsync(t => t.Assignment.LoadId == s.LoadId));
        Assert.Equal(1, await db.Assignments.CountAsync(a => a.LoadId == s.LoadId));
    }

    [Fact]
    public async Task OtherShipper_CannotConfirmTheMatchOfSomeoneElsesLoad()
    {
        var s = await ArrangeThroughAgentPipelineAsync();
        var otherEmail = $"pg-e2e-other-{Guid.NewGuid():N}@example.com";
        (await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = otherEmail, Password = Password, FullName = "Other Shipper", CompanyName = "Other Co", BillingAddress = "2 Test Lane, Colombo"
        })).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = otherEmail, Password = Password });
        var otherToken = (await login.Content.ReadFromJsonAsync<TokenResponseDto>())!.AccessToken;

        var confirm = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/v1/loads/{s.LoadId}/match/confirm", otherToken,
            new ConfirmMatchDto { AgencyId = s.AgencyId }));

        Assert.True(confirm.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"Got {(int)confirm.StatusCode}");
        await using var db = _factory.CreateDbContext();
        Assert.False(await db.Assignments.AnyAsync(a => a.LoadId == s.LoadId));
    }
}
