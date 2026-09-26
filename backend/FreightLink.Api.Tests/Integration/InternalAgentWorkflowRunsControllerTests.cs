using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.DTOs.Internal.Candidates;
using FreightLink.Api.DTOs.Internal.ToolCalls;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

public class InternalAgentWorkflowRunsControllerTests
{
    private const string ValidKey = CustomWebApplicationFactory.ValidInternalApiKey;

    private static async Task<TokenResponseDto> RegisterAndLoginShipperAsync(HttpClient client, string emailPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecret1";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = email,
            Password = password,
            FullName = "Agent Tester",
            CompanyName = "Lanka Freight",
            BillingAddress = "123 Port Road, Colombo"
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    private static async Task<(LoadResponseDto Load, Guid UserId)> SeedLoadAsync(HttpClient client)
    {
        var tokens = await RegisterAndLoginShipperAsync(client, "agent-tester");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/loads");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Content = JsonContent.Create(new CreateLoadDto
        {
            CargoDescription = "Electrical transformers",
            WeightKg = 3500m,
            VolumeM3 = 10m,
            PickupAddress = "Kelaniya Factory, Colombo",
            PickupLat = 6.9535m,
            PickupLng = 79.9182m,
            DropoffAddress = "Peradeniya Station, Kandy",
            DropoffLat = 7.2605m,
            DropoffLng = 80.5969m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(2),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(8)
        });

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var load = (await response.Content.ReadFromJsonAsync<LoadResponseDto>())!;
        return (load, load.ShipperUserId);
    }

    [Fact]
    public async Task RecordToolCall_WithoutApiKey_Returns401()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/internal/agent-workflow-runs/{Guid.NewGuid()}/tool-calls", new CreateToolCallRequestDto
        {
            ToolName = ToolName.get_route_and_eta,
            AttemptNo = 1
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RecordToolCall_WithValidApiKey_PersistsAndReturns201()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var (load, userId) = await SeedLoadAsync(client);

        // 1. Create a workflow run
        using var createRunMsg = new HttpRequestMessage(HttpMethod.Post, "/internal/agent-workflow-runs");
        createRunMsg.Headers.Add("X-Internal-Api-Key", ValidKey);
        createRunMsg.Content = JsonContent.Create(new CreateAgentWorkflowRunRequestDto
        {
            LoadId = load.LoadId,
            TriggeredByUserId = userId,
            AttemptNo = 1
        });
        var createRunRes = await client.SendAsync(createRunMsg);
        createRunRes.EnsureSuccessStatusCode();
        var run = (await createRunRes.Content.ReadFromJsonAsync<CreateAgentWorkflowRunResponseDto>())!;

        // 2. Record a ToolCall under the run
        using var toolCallMsg = new HttpRequestMessage(HttpMethod.Post, $"/internal/agent-workflow-runs/{run.WorkflowRunId}/tool-calls");
        toolCallMsg.Headers.Add("X-Internal-Api-Key", ValidKey);
        toolCallMsg.Content = JsonContent.Create(new CreateToolCallRequestDto
        {
            ToolName = ToolName.get_route_and_eta,
            AttemptNo = 1,
            RequestJson = "{\"originLat\":6.9535,\"originLng\":79.9182}",
            ResponseJson = "{\"distanceKm\":112.5,\"etaMinutes\":145}",
            Success = true,
            HttpStatusCode = 200,
            DurationMs = 280,
            CalledAt = DateTimeOffset.UtcNow
        });

        var toolCallRes = await client.SendAsync(toolCallMsg);
        Assert.Equal(HttpStatusCode.Created, toolCallRes.StatusCode);

        var toolCallData = await toolCallRes.Content.ReadFromJsonAsync<ToolCallResponseDto>();
        Assert.NotNull(toolCallData);
        Assert.NotEqual(Guid.Empty, toolCallData.ToolCallId);
        Assert.NotEqual(Guid.Empty, toolCallData.AgentStepId);
        Assert.Equal(ToolName.get_route_and_eta, toolCallData.ToolName);
        Assert.True(toolCallData.Success);
    }

    [Fact]
    public async Task RecordToolCall_FailureWithoutErrorMessage_Returns400()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var (load, userId) = await SeedLoadAsync(client);

        using var createRunMsg = new HttpRequestMessage(HttpMethod.Post, "/internal/agent-workflow-runs");
        createRunMsg.Headers.Add("X-Internal-Api-Key", ValidKey);
        createRunMsg.Content = JsonContent.Create(new CreateAgentWorkflowRunRequestDto
        {
            LoadId = load.LoadId,
            TriggeredByUserId = userId,
            AttemptNo = 1
        });
        var createRunRes = await client.SendAsync(createRunMsg);
        createRunRes.EnsureSuccessStatusCode();
        var run = (await createRunRes.Content.ReadFromJsonAsync<CreateAgentWorkflowRunResponseDto>())!;

        using var toolCallMsg = new HttpRequestMessage(HttpMethod.Post, $"/internal/agent-workflow-runs/{run.WorkflowRunId}/tool-calls");
        toolCallMsg.Headers.Add("X-Internal-Api-Key", ValidKey);
        toolCallMsg.Content = JsonContent.Create(new CreateToolCallRequestDto
        {
            ToolName = ToolName.get_route_and_eta,
            AttemptNo = 1,
            Success = false, // Failure requires ErrorMessage
            ErrorMessage = null
        });

        var toolCallRes = await client.SendAsync(toolCallMsg);
        Assert.Equal(HttpStatusCode.BadRequest, toolCallRes.StatusCode);
    }

    [Fact]
    public async Task ReportStep_PlannerStep1_UpdatesObjectiveAndPlanJson()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var (load, userId) = await SeedLoadAsync(client);

        // 1. Create a workflow run
        using var createRunMsg = new HttpRequestMessage(HttpMethod.Post, "/internal/agent-workflow-runs");
        createRunMsg.Headers.Add("X-Internal-Api-Key", ValidKey);
        createRunMsg.Content = JsonContent.Create(new CreateAgentWorkflowRunRequestDto
        {
            LoadId = load.LoadId,
            TriggeredByUserId = userId,
            AttemptNo = 1
        });
        var createRunRes = await client.SendAsync(createRunMsg);
        createRunRes.EnsureSuccessStatusCode();
        var run = (await createRunRes.Content.ReadFromJsonAsync<CreateAgentWorkflowRunResponseDto>())!;

        // 2. Report Step 1 (Planner)
        using var step1Msg = new HttpRequestMessage(HttpMethod.Post, $"/internal/agent-workflow-runs/{run.WorkflowRunId}/steps");
        step1Msg.Headers.Add("X-Internal-Api-Key", ValidKey);
        step1Msg.Content = JsonContent.Create(new ReportAgentStepRequestDto
        {
            StepNo = 1,
            AgentRole = AgentRole.Planner,
            Status = AgentStepStatus.Succeeded,
            InputJson = "{\"loadId\":\"" + load.LoadId + "\"}",
            OutputJson = "{\"objective\":\"Find and assign optimal carrier for transformer\",\"steps\":[\"Evaluate candidate agencies\"]}",
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-2),
            CompletedAt = DateTimeOffset.UtcNow,
            DurationMs = 2000
        });

        var step1Res = await client.SendAsync(step1Msg);
        Assert.Equal(HttpStatusCode.Created, step1Res.StatusCode);

        var stepData = await step1Res.Content.ReadFromJsonAsync<ReportAgentStepResponseDto>();
        Assert.NotNull(stepData);
        Assert.NotEqual(Guid.Empty, stepData.AgentStepId);
        Assert.Equal(AgentStepStatus.Succeeded, stepData.Status);

        // Verify in DB
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbRun = await db.AgentWorkflowRuns.Include(r => r.Steps).FirstAsync(r => r.WorkflowRunId == run.WorkflowRunId);
        Assert.Equal("Find and assign optimal carrier for transformer", dbRun.Objective);
        Assert.NotNull(dbRun.PlanJson);
        Assert.Single(dbRun.Steps);
        Assert.Equal(1, dbRun.Steps.First().StepNo);
        Assert.Equal(AgentRole.Planner, dbRun.Steps.First().AgentRole);
    }

    [Fact]
    public async Task ReportStep_DomainAnalysisStep2_SyncsCandidatesToDatabase()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var (load, userId) = await SeedLoadAsync(client);

        // Seed an active agency in DB
        var agencyId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Agencies.Add(new Agency
            {
                AgencyId = agencyId,
                Name = "Apex Freight Terminal",
                BusinessRegNo = $"APX-{Guid.NewGuid():N}"[..12],
                YardAddress = "12 Industrial Road, Colombo",
                YardLat = 6.9319m,
                YardLng = 79.8478m,
                Status = AgencyStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // 1. Create a workflow run
        using var createRunMsg = new HttpRequestMessage(HttpMethod.Post, "/internal/agent-workflow-runs");
        createRunMsg.Headers.Add("X-Internal-Api-Key", ValidKey);
        createRunMsg.Content = JsonContent.Create(new CreateAgentWorkflowRunRequestDto
        {
            LoadId = load.LoadId,
            TriggeredByUserId = userId,
            AttemptNo = 1
        });
        var createRunRes = await client.SendAsync(createRunMsg);
        createRunRes.EnsureSuccessStatusCode();
        var run = (await createRunRes.Content.ReadFromJsonAsync<CreateAgentWorkflowRunResponseDto>())!;

        // 2. Report Step 2 (DomainAnalysis) with candidate array in OutputJson
        var step2Output = "{\"shortlistCount\":1,\"candidates\":[{\"agencyId\":\"" + agencyId + "\",\"agencyName\":\"Apex Freight Terminal\",\"rank\":1,\"eligible\":true,\"eligibilityScore\":95.0}]}";

        using var step2Msg = new HttpRequestMessage(HttpMethod.Post, $"/internal/agent-workflow-runs/{run.WorkflowRunId}/steps");
        step2Msg.Headers.Add("X-Internal-Api-Key", ValidKey);
        step2Msg.Content = JsonContent.Create(new ReportAgentStepRequestDto
        {
            StepNo = 2,
            AgentRole = AgentRole.DomainAnalysis,
            Status = AgentStepStatus.Succeeded,
            InputJson = "{\"loadId\":\"" + load.LoadId + "\",\"candidatesEvaluatedCount\":1}",
            OutputJson = step2Output,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAt = DateTimeOffset.UtcNow,
            DurationMs = 1000
        });

        var step2Res = await client.SendAsync(step2Msg);
        Assert.Equal(HttpStatusCode.Created, step2Res.StatusCode);

        // Verify in DB that MatchCandidate was synced
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var candidates = await db.MatchCandidates.Where(mc => mc.WorkflowRunId == run.WorkflowRunId).ToListAsync();
            Assert.Single(candidates);
            Assert.Equal(agencyId, candidates[0].AgencyId);
            Assert.Equal(1, candidates[0].Rank);
            Assert.True(candidates[0].Eligible);
            Assert.Equal(95.0m, candidates[0].EligibilityScore);
        }
    }

    [Fact]
    public async Task ReportStep_ValidationSafetyStep4_TransitionsWorkflowRunToAwaitingApproval()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var (load, userId) = await SeedLoadAsync(client);

        // 1. Create a workflow run
        using var createRunMsg = new HttpRequestMessage(HttpMethod.Post, "/internal/agent-workflow-runs");
        createRunMsg.Headers.Add("X-Internal-Api-Key", ValidKey);
        createRunMsg.Content = JsonContent.Create(new CreateAgentWorkflowRunRequestDto
        {
            LoadId = load.LoadId,
            TriggeredByUserId = userId,
            AttemptNo = 1
        });
        var createRunRes = await client.SendAsync(createRunMsg);
        createRunRes.EnsureSuccessStatusCode();
        var run = (await createRunRes.Content.ReadFromJsonAsync<CreateAgentWorkflowRunResponseDto>())!;

        // 2. Report Step 4 (ValidationSafety) as Succeeded
        using var step4Msg = new HttpRequestMessage(HttpMethod.Post, $"/internal/agent-workflow-runs/{run.WorkflowRunId}/steps");
        step4Msg.Headers.Add("X-Internal-Api-Key", ValidKey);
        step4Msg.Content = JsonContent.Create(new ReportAgentStepRequestDto
        {
            StepNo = 4,
            AgentRole = AgentRole.ValidationSafety,
            Status = AgentStepStatus.Succeeded,
            InputJson = "{\"loadId\":\"" + load.LoadId + "\"}",
            OutputJson = "{\"recommendation\":\"Approve\",\"checks\":[]}",
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAt = DateTimeOffset.UtcNow,
            DurationMs = 500
        });

        var step4Res = await client.SendAsync(step4Msg);
        Assert.Equal(HttpStatusCode.Created, step4Res.StatusCode);

        // Verify in DB that run transitioned to AwaitingApproval
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbRun = await db.AgentWorkflowRuns.FirstAsync(r => r.WorkflowRunId == run.WorkflowRunId);
        Assert.Equal(WorkflowRunStatus.AwaitingApproval, dbRun.Status);
    }

    [Fact]
    public async Task RecordMatchCandidates_WithValidApiKey_PersistsCandidatesDirectly()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var (load, userId) = await SeedLoadAsync(client);

        var agencyId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Agencies.Add(new Agency
            {
                AgencyId = agencyId,
                Name = "Direct Candidate Agency",
                BusinessRegNo = $"DIR-{Guid.NewGuid():N}"[..12],
                YardAddress = "50 Ocean Ave, Colombo",
                YardLat = 6.9271m,
                YardLng = 79.8612m,
                Status = AgencyStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // 1. Create a workflow run
        using var createRunMsg = new HttpRequestMessage(HttpMethod.Post, "/internal/agent-workflow-runs");
        createRunMsg.Headers.Add("X-Internal-Api-Key", ValidKey);
        createRunMsg.Content = JsonContent.Create(new CreateAgentWorkflowRunRequestDto
        {
            LoadId = load.LoadId,
            TriggeredByUserId = userId,
            AttemptNo = 1
        });
        var createRunRes = await client.SendAsync(createRunMsg);
        createRunRes.EnsureSuccessStatusCode();
        var run = (await createRunRes.Content.ReadFromJsonAsync<CreateAgentWorkflowRunResponseDto>())!;

        // 2. POST candidates directly
        using var candMsg = new HttpRequestMessage(HttpMethod.Post, $"/internal/agent-workflow-runs/{run.WorkflowRunId}/candidates");
        candMsg.Headers.Add("X-Internal-Api-Key", ValidKey);
        candMsg.Content = JsonContent.Create(new List<MatchCandidateDto>
        {
            new MatchCandidateDto
            {
                AgencyId = agencyId,
                Rank = 1,
                Eligible = true,
                EligibilityScore = 98.5m
            }
        });

        var candRes = await client.SendAsync(candMsg);
        Assert.Equal(HttpStatusCode.OK, candRes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var candidate = await db.MatchCandidates.FirstOrDefaultAsync(mc => mc.WorkflowRunId == run.WorkflowRunId && mc.AgencyId == agencyId);
            Assert.NotNull(candidate);
            Assert.Equal(1, candidate.Rank);
            Assert.True(candidate.Eligible);
            Assert.Equal(98.5m, candidate.EligibilityScore);
        }
    }
}
