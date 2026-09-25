using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.DTOs.Internal.ToolCalls;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities.Enums;
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
}
