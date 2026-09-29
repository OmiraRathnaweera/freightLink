using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

public class MatchConfirmControllerTests
{
    private static async Task<TokenResponseDto> RegisterAndLoginShipperAsync(HttpClient client, string emailPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecret1";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = email,
            Password = password,
            FullName = "Match Shipper",
            CompanyName = "Shipper Corp",
            BillingAddress = "100 Port Road, Colombo"
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    private static async Task<Guid> SeedAgencyAsync(CustomWebApplicationFactory factory, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = name,
            BusinessRegNo = $"PV-{Guid.NewGuid():N}"[..12],
            YardAddress = "123 Yard Road, Colombo",
            YardLat = 6.9271m,
            YardLng = 79.8612m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Agencies.Add(agency);
        await db.SaveChangesAsync();
        return agency.AgencyId;
    }

    private static async Task<LoadResponseDto> SeedLoadAsync(HttpClient client, TokenResponseDto tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/loads");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Content = JsonContent.Create(new CreateLoadDto
        {
            CargoDescription = "Industrial machine parts",
            WeightKg = 2500m,
            VolumeM3 = 8m,
            PickupAddress = "Kelaniya Depot",
            PickupLat = 6.9535m,
            PickupLng = 79.9182m,
            DropoffAddress = "Peradeniya Zone",
            DropoffLat = 7.2605m,
            DropoffLng = 80.5969m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(2),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(8)
        });

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoadResponseDto>())!;
    }

    [Fact]
    public async Task ConfirmMatch_Candidate1_ReusesExistingPrice_CreatesProposedAssignment_CompletesRun()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var tokens = await RegisterAndLoginShipperAsync(client, "shipper-cand1");
        var load = await SeedLoadAsync(client, tokens);
        var agency1Id = await SeedAgencyAsync(factory, "Lanka Express Logistics");

        var workflowRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Seed workflow run in AwaitingApproval status with Candidate 1 and Step 3
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Update load estimated price
            var dbLoad = await db.Loads.FirstAsync(l => l.LoadId == load.LoadId);
            dbLoad.EstimatedPrice = 18500m;

            var run = new AgentWorkflowRun
            {
                WorkflowRunId = workflowRunId,
                LoadId = load.LoadId,
                TriggeredByUserId = load.ShipperUserId,
                AttemptNo = 1,
                Objective = "Find best carrier and price load",
                Status = WorkflowRunStatus.AwaitingApproval,
                StartedAt = now.AddMinutes(-5),
                CreatedAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-1)
            };
            db.AgentWorkflowRuns.Add(run);

            var candidate1 = new MatchCandidate
            {
                MatchCandidateId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                AgencyId = agency1Id,
                Rank = 1,
                EligibilityScore = 0.95m,
                Eligible = true,
                EvaluatedAt = now.AddMinutes(-4)
            };
            db.MatchCandidates.Add(candidate1);

            var step3 = new AgentStep
            {
                AgentStepId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                StepNo = 3,
                AgentRole = AgentRole.MatchingPricing,
                Status = AgentStepStatus.Succeeded,
                OutputJson = JsonSerializer.Serialize(new
                {
                    selectedAgencyId = agency1Id.ToString(),
                    suggestedVehicleClass = "MediumLorry",
                    etaMinutes = 35,
                    cargoDistanceKm = 115.0,
                    proposedPrice = 18500.0
                }),
                StartedAt = now.AddMinutes(-3),
                CompletedAt = now.AddMinutes(-2)
            };
            db.AgentSteps.Add(step3);

            await db.SaveChangesAsync();
        }

        // Shipper confirms candidate 1
        using var confirmRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/match/confirm");
        confirmRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        confirmRequest.Content = JsonContent.Create(new ConfirmMatchDto { AgencyId = agency1Id });

        var response = await client.SendAsync(confirmRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var assignment = await response.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(assignment);
        Assert.Equal(agency1Id, assignment.AgencyId);
        Assert.Equal(AssignmentStatus.Proposed.ToString(), assignment.Status);
        Assert.Equal(18500m, assignment.ProposedPrice);

        // Verify DB state: ApprovalDecision created and WorkflowRun completed
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbRun = await db.AgentWorkflowRuns
                .Include(r => r.ApprovalDecisions)
                .Include(r => r.Assignments)
                .FirstAsync(r => r.WorkflowRunId == workflowRunId);

            Assert.Equal(WorkflowRunStatus.Completed, dbRun.Status);
            Assert.NotNull(dbRun.CompletedAt);
            Assert.Single(dbRun.ApprovalDecisions);
            Assert.Equal(ApprovalDecisionType.Approve, dbRun.ApprovalDecisions.First().Decision);
            Assert.Single(dbRun.Assignments);
            Assert.Equal(AssignmentStatus.Proposed, dbRun.Assignments.First().Status);
        }
    }

    [Fact]
    public async Task ConfirmMatch_DoubleConfirm_Returns409Conflict()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var tokens = await RegisterAndLoginShipperAsync(client, "shipper-double");
        var load = await SeedLoadAsync(client, tokens);
        var agencyId = await SeedAgencyAsync(factory, "Double Confirm Logistics");

        var workflowRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = new AgentWorkflowRun
            {
                WorkflowRunId = workflowRunId,
                LoadId = load.LoadId,
                TriggeredByUserId = load.ShipperUserId,
                AttemptNo = 1,
                Objective = "Match load",
                Status = WorkflowRunStatus.AwaitingApproval,
                StartedAt = now.AddMinutes(-5),
                CreatedAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-1)
            };
            db.AgentWorkflowRuns.Add(run);
            await db.SaveChangesAsync();
        }

        // First confirm: succeeds
        using var firstReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/match/confirm");
        firstReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        firstReq.Content = JsonContent.Create(new ConfirmMatchDto { AgencyId = agencyId });

        var firstResp = await client.SendAsync(firstReq);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        // Second confirm attempt (simulate double-click / race): must be rejected with 409 Conflict
        using var secondReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/match/confirm");
        secondReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        secondReq.Content = JsonContent.Create(new ConfirmMatchDto { AgencyId = agencyId });

        var secondResp = await client.SendAsync(secondReq);
        Assert.Equal(HttpStatusCode.Conflict, secondResp.StatusCode);
    }

    [Fact]
    public async Task ConfirmMatch_NonOwnerShipper_Returns403Forbidden()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerTokens = await RegisterAndLoginShipperAsync(client, "load-owner");
        var otherTokens = await RegisterAndLoginShipperAsync(client, "other-shipper");

        var load = await SeedLoadAsync(client, ownerTokens);
        var agencyId = await SeedAgencyAsync(factory, "Carrier Co");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AgentWorkflowRuns.Add(new AgentWorkflowRun
            {
                WorkflowRunId = Guid.NewGuid(),
                LoadId = load.LoadId,
                TriggeredByUserId = load.ShipperUserId,
                AttemptNo = 1,
                Objective = "Match load",
                Status = WorkflowRunStatus.AwaitingApproval,
                StartedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Attempt confirm by different shipper
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/match/confirm");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherTokens.AccessToken);
        req.Content = JsonContent.Create(new ConfirmMatchDto { AgencyId = agencyId });

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ConfirmMatch_Candidate2_RecallsPricingEstimate_AndCreatesProposedAssignment()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var tokens = await RegisterAndLoginShipperAsync(client, "shipper-cand2");
        var load = await SeedLoadAsync(client, tokens);
        var agency1Id = await SeedAgencyAsync(factory, "Carrier One");
        var agency2Id = await SeedAgencyAsync(factory, "Carrier Two");

        var workflowRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dbLoad = await db.Loads.FirstAsync(l => l.LoadId == load.LoadId);
            dbLoad.EstimatedPrice = 18500m;

            var run = new AgentWorkflowRun
            {
                WorkflowRunId = workflowRunId,
                LoadId = load.LoadId,
                TriggeredByUserId = load.ShipperUserId,
                AttemptNo = 1,
                Objective = "Find best carrier and price load",
                Status = WorkflowRunStatus.AwaitingApproval,
                StartedAt = now.AddMinutes(-5),
                CreatedAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-1)
            };
            db.AgentWorkflowRuns.Add(run);

            // Candidate 1 (Rank 1)
            db.MatchCandidates.Add(new MatchCandidate
            {
                MatchCandidateId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                AgencyId = agency1Id,
                Rank = 1,
                EligibilityScore = 0.95m,
                Eligible = true,
                EvaluatedAt = now.AddMinutes(-4)
            });

            // Candidate 2 (Rank 2)
            db.MatchCandidates.Add(new MatchCandidate
            {
                MatchCandidateId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                AgencyId = agency2Id,
                Rank = 2,
                EligibilityScore = 0.85m,
                Eligible = true,
                EvaluatedAt = now.AddMinutes(-4)
            });

            var step3 = new AgentStep
            {
                AgentStepId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                StepNo = 3,
                AgentRole = AgentRole.MatchingPricing,
                Status = AgentStepStatus.Succeeded,
                OutputJson = JsonSerializer.Serialize(new
                {
                    selectedAgencyId = agency1Id.ToString(),
                    suggestedVehicleClass = "MiniTruck",
                    etaMinutes = 45,
                    cargoDistanceKm = 120.0,
                    proposedPrice = 18500.0
                }),
                StartedAt = now.AddMinutes(-3),
                CompletedAt = now.AddMinutes(-2)
            };
            db.AgentSteps.Add(step3);

            await db.SaveChangesAsync();
        }

        // Shipper picks Candidate 2 instead of Candidate 1
        using var confirmRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/match/confirm");
        confirmRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        confirmRequest.Content = JsonContent.Create(new ConfirmMatchDto { AgencyId = agency2Id });

        var response = await client.SendAsync(confirmRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var assignment = await response.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(assignment);
        Assert.Equal(agency2Id, assignment.AgencyId);
        Assert.Equal(AssignmentStatus.Proposed.ToString(), assignment.Status);
        Assert.True(assignment.ProposedPrice > 0);

        // Verify DB state
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbRun = await db.AgentWorkflowRuns
                .Include(r => r.ApprovalDecisions)
                .Include(r => r.Assignments)
                .FirstAsync(r => r.WorkflowRunId == workflowRunId);

            Assert.Equal(WorkflowRunStatus.Completed, dbRun.Status);
            Assert.Single(dbRun.ApprovalDecisions);
            Assert.Equal(ApprovalDecisionType.Approve, dbRun.ApprovalDecisions.First().Decision);
            Assert.Single(dbRun.Assignments);
            Assert.Equal(agency2Id, dbRun.Assignments.First().AgencyId);
            Assert.Equal(AssignmentStatus.Proposed, dbRun.Assignments.First().Status);
        }
    }

    [Fact]
    public async Task ConfirmMatch_Candidate2_UsesThatCandidatesOwnPositioningEta_NotCandidate1Stale()
    {
        // Regression test: overriding to a different candidate must use THAT candidate's own
        // positioning ETA (from Agent 3's rankedCandidates comparison), never agency #1's
        // leftover ETA copied onto a completely different agency's Assignment row.
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var tokens = await RegisterAndLoginShipperAsync(client, "shipper-cand2-eta");
        var load = await SeedLoadAsync(client, tokens);
        var agency1Id = await SeedAgencyAsync(factory, "Carrier One Eta");
        var agency2Id = await SeedAgencyAsync(factory, "Carrier Two Eta");

        var workflowRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dbLoad = await db.Loads.FirstAsync(l => l.LoadId == load.LoadId);
            dbLoad.EstimatedPrice = 18500m;

            var run = new AgentWorkflowRun
            {
                WorkflowRunId = workflowRunId,
                LoadId = load.LoadId,
                TriggeredByUserId = load.ShipperUserId,
                AttemptNo = 1,
                Objective = "Find best carrier and price load",
                Status = WorkflowRunStatus.AwaitingApproval,
                StartedAt = now.AddMinutes(-5),
                CreatedAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-1)
            };
            db.AgentWorkflowRuns.Add(run);

            db.MatchCandidates.Add(new MatchCandidate
            {
                MatchCandidateId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                AgencyId = agency1Id,
                Rank = 1,
                EligibilityScore = 0.95m,
                Eligible = true,
                EvaluatedAt = now.AddMinutes(-4)
            });
            db.MatchCandidates.Add(new MatchCandidate
            {
                MatchCandidateId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                AgencyId = agency2Id,
                Rank = 2,
                EligibilityScore = 0.85m,
                Eligible = true,
                EvaluatedAt = now.AddMinutes(-4)
            });

            var step3 = new AgentStep
            {
                AgentStepId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                StepNo = 3,
                AgentRole = AgentRole.MatchingPricing,
                Status = AgentStepStatus.Succeeded,
                OutputJson = JsonSerializer.Serialize(new
                {
                    selectedAgencyId = agency1Id.ToString(),
                    suggestedVehicleClass = "MiniTruck",
                    etaMinutes = 12, // agency #1's own positioning ETA - must NOT end up on agency #2's Assignment
                    cargoDistanceKm = 120.0,
                    proposedPrice = 18500.0,
                    rankedCandidates = new[]
                    {
                        new { agencyId = agency1Id.ToString(), etaMinutes = 12, distanceKm = 8.0 },
                        new { agencyId = agency2Id.ToString(), etaMinutes = 47, distanceKm = 61.0 },
                    }
                }),
                StartedAt = now.AddMinutes(-3),
                CompletedAt = now.AddMinutes(-2)
            };
            db.AgentSteps.Add(step3);

            await db.SaveChangesAsync();
        }

        using var confirmRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/match/confirm");
        confirmRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        confirmRequest.Content = JsonContent.Create(new ConfirmMatchDto { AgencyId = agency2Id });

        var response = await client.SendAsync(confirmRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var assignment = await response.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(assignment);
        Assert.Equal(agency2Id, assignment!.AgencyId);
        // Agency #2's own ETA (47), never agency #1's (12).
        Assert.Equal(47, assignment.ProposedEtaMinutes);
    }

    [Fact]
    public async Task ConfirmMatch_ByOwningShipper_Returns200Ok_AndCompletesWorkflowRun()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var tokens = await RegisterAndLoginShipperAsync(client, "shipper-owning-test");
        var load = await SeedLoadAsync(client, tokens);
        var agencyId = await SeedAgencyAsync(factory, "Owner Approved Carrier");

        var workflowRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dbLoad = await db.Loads.FirstAsync(l => l.LoadId == load.LoadId);
            dbLoad.EstimatedPrice = 22000m;

            var run = new AgentWorkflowRun
            {
                WorkflowRunId = workflowRunId,
                LoadId = load.LoadId,
                TriggeredByUserId = load.ShipperUserId,
                AttemptNo = 1,
                Objective = "Find best carrier and price load",
                Status = WorkflowRunStatus.AwaitingApproval,
                StartedAt = now.AddMinutes(-5),
                CreatedAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-1)
            };
            db.AgentWorkflowRuns.Add(run);

            db.MatchCandidates.Add(new MatchCandidate
            {
                MatchCandidateId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                AgencyId = agencyId,
                Rank = 1,
                EligibilityScore = 0.98m,
                Eligible = true,
                EvaluatedAt = now.AddMinutes(-4)
            });

            var step3 = new AgentStep
            {
                AgentStepId = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                StepNo = 3,
                AgentRole = AgentRole.MatchingPricing,
                Status = AgentStepStatus.Succeeded,
                OutputJson = JsonSerializer.Serialize(new
                {
                    selectedAgencyId = agencyId.ToString(),
                    suggestedVehicleClass = "MiniTruck",
                    etaMinutes = 30,
                    cargoDistanceKm = 100.0,
                    proposedPrice = 22000.0
                }),
                StartedAt = now.AddMinutes(-3),
                CompletedAt = now.AddMinutes(-2)
            };
            db.AgentSteps.Add(step3);

            await db.SaveChangesAsync();
        }

        // The owning Shipper confirms the match.
        using var confirmRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/match/confirm");
        confirmRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        confirmRequest.Content = JsonContent.Create(new ConfirmMatchDto { AgencyId = agencyId });

        var response = await client.SendAsync(confirmRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var assignment = await response.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(assignment);
        Assert.Equal(agencyId, assignment.AgencyId);
        Assert.Equal(AssignmentStatus.Proposed.ToString(), assignment.Status);

        // Verify DB state
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbRun = await db.AgentWorkflowRuns
                .Include(r => r.ApprovalDecisions)
                .Include(r => r.Assignments)
                .FirstAsync(r => r.WorkflowRunId == workflowRunId);

            Assert.Equal(WorkflowRunStatus.Completed, dbRun.Status);
            Assert.Single(dbRun.ApprovalDecisions);
            Assert.Equal(ApprovalDecisionType.Approve, dbRun.ApprovalDecisions.First().Decision);
        }
    }

    [Fact]
    public async Task ConfirmMatch_WhenNoPriorWorkflowRun_ReturnsNotFound_NeverFabricatesARun()
    {
        // Confirming a match with no prior AgentWorkflowRun used to synthesize a fake run with
        // four "Succeeded" AgentSteps and mark it Completed - letting a Shipper "confirm" a match
        // no agent ever actually evaluated, while the persisted audit trail claimed otherwise.
        // That was the single most serious finding of the Sep 27 2026 audit
        // (plans/04-backend-integration.md §5). Matching must be triggered for real first.
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var tokens = await RegisterAndLoginShipperAsync(client, "shipper-no-prior-run");
        var load = await SeedLoadAsync(client, tokens);
        var agencyId = await SeedAgencyAsync(factory, "Auto Matched Carrier");

        // Note: No AgentWorkflowRun seeded in DB!
        using var confirmRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/loads/{load.LoadId}/match/confirm");
        confirmRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        confirmRequest.Content = JsonContent.Create(new ConfirmMatchDto { AgencyId = agencyId });

        var response = await client.SendAsync(confirmRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        using var errorDoc = JsonDocument.Parse(raw);
        Assert.Equal("NO_MATCH_RUN_TO_CONFIRM", errorDoc.RootElement.GetProperty("error").GetProperty("code").GetString());

        // Verify DB state: absolutely nothing was fabricated
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbRun = await db.AgentWorkflowRuns.FirstOrDefaultAsync(r => r.LoadId == load.LoadId);
            Assert.Null(dbRun);

            var anyAssignment = await db.Assignments.AnyAsync(a => a.LoadId == load.LoadId);
            Assert.False(anyAssignment);
        }
    }

}
