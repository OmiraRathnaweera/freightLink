using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.PricingConfig;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Tests for the fail-closed auth-key fix (plans/04-backend-integration.md §6): the backend must
/// never fall back to a hardcoded default AGENT_SERVICE_API_KEY when it's missing/misconfigured.
/// </summary>
public class AssignmentServiceTriggerMatchTests
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

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task TriggerMatchAsync_ThrowsAgentServiceNotConfigured_WhenApiKeyMissing_RatherThanUsingHardcodedFallback()
    {
        var db = CreateContext();
        var shipperId = Guid.NewGuid();
        db.Users.Add(new User
        {
            UserId = shipperId,
            Role = UserRole.Shipper,
            Email = "shipper@freightlink.lk",
            PasswordHash = "hash",
            FullName = "Shipper User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var loadId = Guid.NewGuid();
        db.Loads.Add(new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperId,
            ReferenceCode = "LD-TEST-002",
            PickupAddress = "Colombo Port",
            PickupLat = 6.9400m,
            PickupLng = 79.8500m,
            DropoffAddress = "Kandy Central",
            DropoffLat = 7.2900m,
            DropoffLng = 80.6300m,
            WeightKg = 2500m,
            VolumeM3 = 8m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // Deliberately empty configuration: no AGENT_SERVICE_API_KEY anywhere.
        var emptyConfiguration = new ConfigurationBuilder().Build();

        var pricingConfigService = new PricingConfigService(db);
        var pricingEstimator = new PricingEstimatorService(db, pricingConfigService);
        var sut = new AssignmentService(db, new FakeEmailService(), pricingEstimator, routeService: null, configuration: emptyConfiguration);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => sut.TriggerMatchAsync(loadId, shipperId, UserRole.Shipper));

        Assert.Equal(ErrorCode.AGENT_SERVICE_NOT_CONFIGURED, ex.Code);

        // Confirm no AgentWorkflowRun was created despite the failure - an honest failure, not a
        // partially-fabricated one.
        var run = await db.AgentWorkflowRuns.FirstOrDefaultAsync(r => r.LoadId == loadId);
        Assert.Null(run);
    }
}
