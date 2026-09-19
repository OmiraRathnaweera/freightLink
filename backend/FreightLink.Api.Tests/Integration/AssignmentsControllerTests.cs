using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

public class AssignmentsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AssignmentsControllerTests(CustomWebApplicationFactory factory)
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
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GetList_ReturnsUnauthorized_WhenNoToken()
    {
        var response = await _client.GetAsync("/api/v1/assignments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetList_ReturnsOk_WhenAuthenticatedAsAgencyStaff()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Test Logistics Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "100 Port Road",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Agencies.Add(agency);

        var staffUser = new User
        {
            UserId = staffUserId,
            FullName = "Staff Member",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffUser);

        var staff = new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgencyStaff.Add(staff);

        var shipperUserId = Guid.NewGuid();
        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Shipper Member",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Test Cargo For Assignment",
            WeightKg = 3000,
            VolumeM3 = 12,
            PickupAddress = "Origin Yard",
            DropoffAddress = "Dest Yard",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffLat = 7.2m,
            DropoffLng = 80.6m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AttemptNo = 1,
            Status = WorkflowRunStatus.Completed,
            TriggeredByUserId = shipperUserId,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 45000m,
            RoutedDistanceKm = 115m,
            ProposedEtaMinutes = 180,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var token = MintToken(staffUserId, UserRole.AgencyStaff);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/assignments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedAssignmentResponseDto>();
        Assert.NotNull(paged);
        Assert.Contains(paged.Items, i => i.AssignmentId == assignment.AssignmentId);
    }

    [Fact]
    public async Task GetById_ReturnsOk_WithFullDetails()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Detail Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "100 Port Road",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Agencies.Add(agency);

        var staffUser = new User
        {
            UserId = staffUserId,
            FullName = "Staff Member",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffUser);

        var staff = new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgencyStaff.Add(staff);

        var shipperUserId = Guid.NewGuid();
        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Shipper Member",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var load = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Detailed Cargo",
            WeightKg = 2500,
            VolumeM3 = 10,
            PickupAddress = "Detailed Pickup",
            DropoffAddress = "Detailed Dropoff",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffLat = 7.2m,
            DropoffLng = 80.6m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AttemptNo = 1,
            Status = WorkflowRunStatus.Completed,
            TriggeredByUserId = shipperUserId,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = load.LoadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 52000m,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var token = MintToken(staffUserId, UserRole.AgencyStaff);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/assignments/{assignment.AssignmentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(detail);
        Assert.Equal(assignment.AssignmentId, detail.AssignmentId);
        Assert.Equal("Detailed Cargo", detail.CargoDescription);
        Assert.Equal(52000m, detail.ProposedPrice);
    }

    [Fact]
    public async Task GetVehiclesAndDrivers_ReturnsFleetForAgency()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Fleet Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Yard",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Agencies.Add(agency);

        var staff = new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgencyStaff.Add(staff);

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agencyId,
            RegistrationNo = $"WP-TEST-{Guid.NewGuid():N}"[..10],
            VehicleType = VehicleType.Lorry,
            CapacityKg = 7000,
            VolumeM3 = 30,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Vehicles.Add(vehicle);

        var driverUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Test Fleet Driver",
            Email = $"driver-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Driver,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(driverUser);

        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            UserId = driverUser.UserId,
            AgencyId = agencyId,
            LicenceNo = "LIC-12345",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Drivers.Add(driver);
        await db.SaveChangesAsync();

        var token = MintToken(staffUserId, UserRole.AgencyStaff);

        // Test vehicles endpoint
        var vReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/agencies/{agencyId}/vehicles");
        vReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var vRes = await _client.SendAsync(vReq);
        Assert.Equal(HttpStatusCode.OK, vRes.StatusCode);
        var vehicles = await vRes.Content.ReadFromJsonAsync<List<VehicleResponseDto>>();
        Assert.NotNull(vehicles);
        Assert.Contains(vehicles, v => v.VehicleId == vehicle.VehicleId);

        // Test drivers endpoint
        var dReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/agencies/{agencyId}/drivers");
        dReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var dRes = await _client.SendAsync(dReq);
        Assert.Equal(HttpStatusCode.OK, dRes.StatusCode);
        var drivers = await dRes.Content.ReadFromJsonAsync<List<DriverResponseDto>>();
        Assert.NotNull(drivers);
        Assert.Contains(drivers, d => d.DriverId == driver.DriverId);

        // Test my fleet endpoint
        var fReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/agencies/my/fleet");
        fReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var fRes = await _client.SendAsync(fReq);
        Assert.Equal(HttpStatusCode.OK, fRes.StatusCode);
        var fleet = await fRes.Content.ReadFromJsonAsync<AgencyFleetResponseDto>();
        Assert.NotNull(fleet);
        Assert.Contains(fleet.Vehicles, v => v.VehicleId == vehicle.VehicleId);
        Assert.Contains(fleet.Drivers, d => d.DriverId == driver.DriverId);
    }
}
