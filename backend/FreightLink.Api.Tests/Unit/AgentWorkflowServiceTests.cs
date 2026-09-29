using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using FreightLink.Api.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AgentWorkflowService"/>, which backs the internal
/// <c>/internal/agent-workflow-runs</c> endpoints the Python agent service calls to record its own
/// run/step/tool-call audit trail. This service performs no auth itself (that's <c>InternalApiKeyAuthFilter</c>'s
/// job upstream), so tests call it directly. Most tests use a lightweight InMemory context; the two
/// duplicate-detection tests need <see cref="PostgresWebApplicationFactory"/> instead, since the
/// service detects duplicates by catching a real Postgres unique-constraint violation
/// (<c>uq_awr_load_attempt</c> / <c>ck_agentstep_stepno</c>-backed unique index), which EF Core's
/// InMemory provider does not enforce.
/// </summary>
public class AgentWorkflowServiceTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _postgresFactory;

    public AgentWorkflowServiceTests(PostgresWebApplicationFactory postgresFactory)
    {
        _postgresFactory = postgresFactory;
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AgentWorkflowService CreateSut(AppDbContext db) => new(db);

    private static async Task<(Guid LoadId, Guid ShipperUserId)> SeedLoadAsync(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var shipper = new User { UserId = Guid.NewGuid(), Role = UserRole.Shipper, Email = $"s-{Guid.NewGuid():N}@example.com", PasswordHash = "h", FullName = "Shipper", IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.Users.Add(shipper);

        var load = new Load
        {
            LoadId = Guid.NewGuid(), ShipperUserId = shipper.UserId, ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Cargo", WeightKg = 300m, VolumeM3 = 2m,
            PickupAddress = "A", PickupLat = 6.9m, PickupLng = 79.8m,
            DropoffAddress = "B", DropoffLat = 7.0m, DropoffLng = 80.0m,
            PickupWindowStart = now, PickupWindowEnd = now.AddHours(2),
            Status = LoadStatus.Posted, CreatedAt = now, UpdatedAt = now
        };
        db.Loads.Add(load);
        await db.SaveChangesAsync();
        return (load.LoadId, shipper.UserId);
    }

    [Fact]
    public async Task CreateAsync_WithValidLoadAndUser_CreatesRun_AndReturnsItsId()
    {
        var db = CreateContext();
        var (loadId, shipperId) = await SeedLoadAsync(db);
        var sut = CreateSut(db);

        var result = await sut.CreateAsync(new CreateAgentWorkflowRunRequestDto { LoadId = loadId, TriggeredByUserId = shipperId, AttemptNo = 1 });

        Assert.NotEqual(Guid.Empty, result.WorkflowRunId);
        var persisted = await db.AgentWorkflowRuns.FindAsync(result.WorkflowRunId);
        Assert.NotNull(persisted);
        Assert.Equal(loadId, persisted!.LoadId);
        Assert.Equal(1, persisted.AttemptNo);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentLoad_ThrowsNotFound()
    {
        var db = CreateContext();
        var shipperId = (await SeedLoadAsync(db)).ShipperUserId;
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.CreateAsync(new CreateAgentWorkflowRunRequestDto { LoadId = Guid.NewGuid(), TriggeredByUserId = shipperId, AttemptNo = 1 }));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateLoadAndAttemptNo_ThrowsConflict()
    {
        var db = _postgresFactory.CreateDbContext();
        var (loadId, shipperId) = await SeedLoadAsync(db);
        var sut = CreateSut(db);

        await sut.CreateAsync(new CreateAgentWorkflowRunRequestDto { LoadId = loadId, TriggeredByUserId = shipperId, AttemptNo = 1 });

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.CreateAsync(new CreateAgentWorkflowRunRequestDto { LoadId = loadId, TriggeredByUserId = shipperId, AttemptNo = 1 }));

        Assert.Equal(ErrorCode.WORKFLOW_RUN_DUPLICATE_ATTEMPT, ex.Code);
    }

    [Fact]
    public async Task ReportStepAsync_PlannerSucceeded_WritesObjectiveAndPlanJson_OnTheParentRun()
    {
        var db = CreateContext();
        var (loadId, shipperId) = await SeedLoadAsync(db);
        var sut = CreateSut(db);
        var run = await sut.CreateAsync(new CreateAgentWorkflowRunRequestDto { LoadId = loadId, TriggeredByUserId = shipperId, AttemptNo = 1 });

        var now = DateTimeOffset.UtcNow;
        await sut.ReportStepAsync(run.WorkflowRunId, new ReportAgentStepRequestDto
        {
            StepNo = 1,
            AgentRole = AgentRole.Planner,
            Status = AgentStepStatus.Succeeded,
            OutputJson = "{\"objective\":\"Match this load\",\"steps\":[\"Planner\"]}",
            StartedAt = now,
            CompletedAt = now.AddSeconds(2)
        });

        var persistedRun = await db.AgentWorkflowRuns.FindAsync(run.WorkflowRunId);
        Assert.NotNull(persistedRun);
        Assert.False(string.IsNullOrEmpty(persistedRun!.PlanJson));
    }

    [Fact]
    public async Task ReportStepAsync_Failed_FlipsParentRunStatusToFailed()
    {
        var db = CreateContext();
        var (loadId, shipperId) = await SeedLoadAsync(db);
        var sut = CreateSut(db);
        var run = await sut.CreateAsync(new CreateAgentWorkflowRunRequestDto { LoadId = loadId, TriggeredByUserId = shipperId, AttemptNo = 1 });

        var now = DateTimeOffset.UtcNow;
        await sut.ReportStepAsync(run.WorkflowRunId, new ReportAgentStepRequestDto
        {
            StepNo = 1,
            AgentRole = AgentRole.Planner,
            Status = AgentStepStatus.Failed,
            ErrorMessage = "LLM call failed",
            StartedAt = now,
            CompletedAt = now.AddSeconds(1)
        });

        var persistedRun = await db.AgentWorkflowRuns.FindAsync(run.WorkflowRunId);
        Assert.Equal(WorkflowRunStatus.Failed, persistedRun!.Status);
    }

    [Fact]
    public async Task ReportStepAsync_FailedWithoutErrorMessage_ThrowsValidationError()
    {
        var db = CreateContext();
        var (loadId, shipperId) = await SeedLoadAsync(db);
        var sut = CreateSut(db);
        var run = await sut.CreateAsync(new CreateAgentWorkflowRunRequestDto { LoadId = loadId, TriggeredByUserId = shipperId, AttemptNo = 1 });

        var now = DateTimeOffset.UtcNow;
        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ReportStepAsync(run.WorkflowRunId, new ReportAgentStepRequestDto
        {
            StepNo = 1,
            AgentRole = AgentRole.Planner,
            Status = AgentStepStatus.Failed,
            ErrorMessage = null,
            StartedAt = now
        }));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, ex.Code);
    }

    [Fact]
    public async Task ReportStepAsync_ForNonExistentWorkflowRun_ThrowsWorkflowRunNotFound()
    {
        var db = CreateContext();
        var sut = CreateSut(db);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sut.ReportStepAsync(Guid.NewGuid(), new ReportAgentStepRequestDto
        {
            StepNo = 1,
            AgentRole = AgentRole.Planner,
            Status = AgentStepStatus.Succeeded,
            StartedAt = DateTimeOffset.UtcNow
        }));

        Assert.Equal(ErrorCode.WORKFLOW_RUN_NOT_FOUND, ex.Code);
    }

    [Fact]
    public async Task ReportStepAsync_CalledTwiceForSameStepNo_UpsertsInPlace_RatherThanThrowing()
    {
        // AgentWorkflowService.ReportStepAsync looks up an existing AgentStep by (WorkflowRunId,
        // StepNo) first and updates it in place if found — sequential re-reports are idempotent
        // upserts, not AGENT_STEP_DUPLICATE conflicts. AGENT_STEP_DUPLICATE (backed by the real
        // uq_agentstep_order Postgres constraint) only fires on a genuine concurrent-insert race,
        // which this single-threaded test does not exercise.
        var db = _postgresFactory.CreateDbContext();
        var (loadId, shipperId) = await SeedLoadAsync(db);
        var sut = CreateSut(db);
        var run = await sut.CreateAsync(new CreateAgentWorkflowRunRequestDto { LoadId = loadId, TriggeredByUserId = shipperId, AttemptNo = 1 });

        var now = DateTimeOffset.UtcNow;
        var first = await sut.ReportStepAsync(run.WorkflowRunId, new ReportAgentStepRequestDto { StepNo = 1, AgentRole = AgentRole.Planner, Status = AgentStepStatus.Succeeded, StartedAt = now });
        var second = await sut.ReportStepAsync(run.WorkflowRunId, new ReportAgentStepRequestDto { StepNo = 1, AgentRole = AgentRole.Planner, Status = AgentStepStatus.Failed, ErrorMessage = "Retried and failed", StartedAt = now });

        Assert.Equal(first.AgentStepId, second.AgentStepId);
        Assert.Equal(AgentStepStatus.Failed, second.Status);

        var stepCount = await db.AgentSteps.CountAsync(s => s.WorkflowRunId == run.WorkflowRunId && s.StepNo == 1);
        Assert.Equal(1, stepCount);
    }
}
