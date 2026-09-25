using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Disputes;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="Controllers.DisputesController"/> testing HTTP endpoints, routing, and role authorization.
/// </summary>
public class DisputesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DisputesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string MintToken(Guid userId, UserRole role)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-that-is-long-enough-1234567890"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<(User Shipper, Agency Agency, User StaffUser, Trip Trip)> SeedTripDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var shipper = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            FullName = "Integration Shipper",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var staffUser = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.AgencyStaff,
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            FullName = "Integration Staff",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = "Integration Agency",
            BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..12],
            YardAddress = "100 Yard St",
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var staff = new AgencyStaff
        {
            AgencyId = agency.AgencyId,
            UserId = staffUser.UserId,
            JobTitle = "Manager",
            CreatedAt = now,
            UpdatedAt = now
        };

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            RegistrationNo = "WP-INT-2222",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 10000m,
            VolumeM3 = 30m,
            Status = VehicleStatus.Available,
            CreatedAt = now,
            UpdatedAt = now
        };

        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            UserId = Guid.NewGuid(),
            LicenceNo = "DL-INT-2",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipper.UserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..10],
            CargoDescription = "Integration cargo",
            WeightKg = 1000m,
            VolumeM3 = 5m,
            PickupAddress = "Pickup loc",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Dropoff loc",
            DropoffLat = 7.0m,
            DropoffLng = 80.0m,
            PickupWindowStart = now.AddHours(1),
            PickupWindowEnd = now.AddHours(4),
            Status = LoadStatus.Delivered,
            CreatedAt = now,
            UpdatedAt = now
        };

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = load.LoadId,
            TriggeredByUserId = shipper.UserId,
            AttemptNo = 1,
            Objective = "Match load",
            Status = WorkflowRunStatus.Completed,
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AgencyId = agency.AgencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 20000m,
            Status = AssignmentStatus.Accepted,
            CreatedAt = now,
            UpdatedAt = now
        };

        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            AssignmentId = assignment.AssignmentId,
            VehicleId = vehicle.VehicleId,
            DriverId = driver.DriverId,
            Status = TripStatus.Delivered,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.AddRange(shipper, staffUser);
        db.Agencies.Add(agency);
        db.AgencyStaff.Add(staff);
        db.Vehicles.Add(vehicle);
        db.Drivers.Add(driver);
        db.Loads.Add(load);
        db.AgentWorkflowRuns.Add(workflowRun);
        db.Assignments.Add(assignment);
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        return (shipper, agency, staffUser, trip);
    }

    [Fact]
    public async Task PostDispute_WithoutAuth_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/disputes", new CreateDisputeDto
        {
            TripId = Guid.NewGuid(),
            Category = DisputeCategory.Damage,
            Description = "Goods were damaged."
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostDispute_AuthenticatedShipper_CreatesAndReturns201()
    {
        var (shipper, _, _, trip) = await SeedTripDataAsync();
        var token = MintToken(shipper.UserId, UserRole.Shipper);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/disputes")
        {
            Content = JsonContent.Create(new CreateDisputeDto
            {
                TripId = trip.TripId,
                Category = DisputeCategory.Damage,
                Description = "Cargo damaged in transit during heavy rain."
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dispute = await response.Content.ReadFromJsonAsync<DisputeResponseDto>();
        Assert.NotNull(dispute);
        Assert.Equal(trip.TripId, dispute.TripId);
        Assert.Equal(DisputeStatus.Raised, dispute.Status);
    }

    [Fact]
    public async Task ResolveDispute_AdminRole_ResolvesAndReturns200()
    {
        var (shipper, _, _, trip) = await SeedTripDataAsync();
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);
        var adminToken = MintToken(Guid.NewGuid(), UserRole.Admin);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/disputes")
        {
            Content = JsonContent.Create(new CreateDisputeDto
            {
                TripId = trip.TripId,
                Category = DisputeCategory.Damage,
                Description = "Cargo damaged in transit."
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<DisputeResponseDto>())!;
        Assert.Equal(DisputeStatus.Raised, created.Status);

        // Move to UnderReview first
        var reviewReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/disputes/{created.DisputeId}/review");
        reviewReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var reviewRes = await _client.SendAsync(reviewReq);
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);
        var reviewed = (await reviewRes.Content.ReadFromJsonAsync<DisputeResponseDto>())!;
        Assert.Equal(DisputeStatus.UnderReview, reviewed.Status);

        // Then resolve
        var resolveReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/disputes/{created.DisputeId}/resolve")
        {
            Content = JsonContent.Create(new ResolveDisputeDto
            {
                Outcome = DisputeOutcome.Upheld,
                ResolutionNote = "Evidence reviewed and approved."
            })
        };
        resolveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resolveRes = await _client.SendAsync(resolveReq);

        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);
        var resolved = await resolveRes.Content.ReadFromJsonAsync<DisputeResponseDto>();
        Assert.NotNull(resolved);
        Assert.Equal(DisputeStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.Resolution);
        Assert.Equal(DisputeOutcome.Upheld, resolved.Resolution.Outcome);
    }

    [Fact]
    public async Task PatchResolve_DirectFromRaised_Returns400BadRequest()
    {
        var (shipper, _, _, trip) = await SeedTripDataAsync();
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);
        var adminToken = MintToken(Guid.NewGuid(), UserRole.Admin);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/disputes")
        {
            Content = JsonContent.Create(new CreateDisputeDto
            {
                TripId = trip.TripId,
                Category = DisputeCategory.Damage,
                Description = "Cargo damaged in transit."
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<DisputeResponseDto>())!;

        // Attempting direct jump Raised -> Resolved must return 400 Bad Request
        var resolveReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/disputes/{created.DisputeId}/resolve")
        {
            Content = JsonContent.Create(new ResolveDisputeDto
            {
                Outcome = DisputeOutcome.Upheld,
                ResolutionNote = "Skipping review."
            })
        };
        resolveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resolveRes = await _client.SendAsync(resolveReq);

        Assert.Equal(HttpStatusCode.BadRequest, resolveRes.StatusCode);
    }

    [Fact]
    public async Task PatchResolve_MissingResolutionNote_Returns400BadRequest()
    {
        var (shipper, _, _, trip) = await SeedTripDataAsync();
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);
        var adminToken = MintToken(Guid.NewGuid(), UserRole.Admin);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/disputes")
        {
            Content = JsonContent.Create(new CreateDisputeDto
            {
                TripId = trip.TripId,
                Category = DisputeCategory.Damage,
                Description = "Cargo damaged in transit."
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<DisputeResponseDto>())!;

        // Move to review
        var reviewReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/disputes/{created.DisputeId}/review");
        reviewReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await _client.SendAsync(reviewReq);

        // Attempting resolution with null/empty resolutionNote
        var resolveReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/disputes/{created.DisputeId}/resolve")
        {
            Content = JsonContent.Create(new ResolveDisputeDto
            {
                Outcome = DisputeOutcome.Upheld,
                ResolutionNote = "   "
            })
        };
        resolveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resolveRes = await _client.SendAsync(resolveReq);

        Assert.Equal(HttpStatusCode.BadRequest, resolveRes.StatusCode);
    }

    [Fact]
    public async Task PatchReview_FromResolved_Returns400BadRequest()
    {
        var (shipper, _, _, trip) = await SeedTripDataAsync();
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);
        var adminToken = MintToken(Guid.NewGuid(), UserRole.Admin);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/disputes")
        {
            Content = JsonContent.Create(new CreateDisputeDto
            {
                TripId = trip.TripId,
                Category = DisputeCategory.Damage,
                Description = "Cargo damaged in transit."
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<DisputeResponseDto>())!;

        // Move to review
        var reviewReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/disputes/{created.DisputeId}/review");
        reviewReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await _client.SendAsync(reviewReq);

        // Resolve
        var resolveReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/disputes/{created.DisputeId}/resolve")
        {
            Content = JsonContent.Create(new ResolveDisputeDto
            {
                Outcome = DisputeOutcome.Upheld,
                ResolutionNote = "Legitimate claim settled."
            })
        };
        resolveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resolveRes = await _client.SendAsync(resolveReq);
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);

        // Attempt backward jump Resolved -> UnderReview must return 400 Bad Request
        var reopenReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/disputes/{created.DisputeId}/review");
        reopenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var reopenRes = await _client.SendAsync(reopenReq);

        Assert.Equal(HttpStatusCode.BadRequest, reopenRes.StatusCode);
    }
}
