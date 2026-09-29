using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

public class AssignmentsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

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
        var vehicles = await vRes.Content.ReadFromJsonAsync<List<VehicleResponseDto>>(JsonOpts);
        Assert.NotNull(vehicles);
        Assert.Contains(vehicles, v => v.VehicleId == vehicle.VehicleId);

        // Test drivers endpoint
        var dReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/agencies/{agencyId}/drivers");
        dReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var dRes = await _client.SendAsync(dReq);
        Assert.Equal(HttpStatusCode.OK, dRes.StatusCode);
        var drivers = await dRes.Content.ReadFromJsonAsync<List<DriverResponseDto>>(JsonOpts);
        Assert.NotNull(drivers);
        Assert.Contains(drivers, d => d.DriverId == driver.DriverId);

        // Test my fleet endpoint
        var fReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/agencies/my/fleet");
        fReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var fRes = await _client.SendAsync(fReq);
        Assert.Equal(HttpStatusCode.OK, fRes.StatusCode);
        var fleet = await fRes.Content.ReadFromJsonAsync<AgencyFleetResponseDto>(JsonOpts);
        Assert.NotNull(fleet);
        Assert.Contains(fleet.Vehicles, v => v.VehicleId == vehicle.VehicleId);
        Assert.Contains(fleet.Drivers, d => d.DriverId == driver.DriverId);
    }

    [Fact]
    public async Task Approve_ReturnsForbidden_WhenAdminAttemptsToFinalizeAgencyAssignment()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var adminUserId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var adminUser = new User
        {
            UserId = adminUserId,
            FullName = "Admin Operator",
            Email = $"admin-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(adminUser);

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Shipper One",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Lanka Freightways",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Peliyagoda Yard 1",
            YardLat = 6.95m,
            YardLng = 79.88m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Agencies.Add(agency);

        var vehicle = new Vehicle
        {
            VehicleId = vehicleId,
            AgencyId = agencyId,
            RegistrationNo = $"WP-AP-{Guid.NewGuid():N}"[..10],
            VehicleType = VehicleType.Lorry,
            CapacityKg = 6000,
            VolumeM3 = 25,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Vehicles.Add(vehicle);

        var driverUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Assigned Driver",
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
            DriverId = driverId,
            UserId = driverUser.UserId,
            AgencyId = agencyId,
            LicenceNo = "LIC-99881",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Drivers.Add(driver);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-AP-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Industrial Hardware",
            WeightKg = 2500,
            VolumeM3 = 12,
            PickupAddress = "Colombo Port Terminal 2",
            PickupLat = 6.94m,
            PickupLng = 79.85m,
            DropoffAddress = "Kandy Industrial Zone",
            DropoffLat = 7.29m,
            DropoffLng = 80.63m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(2),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(6),
            EstimatedPrice = 45000m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = workflowRunId,
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match load to optimal agency",
            Status = WorkflowRunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRunId,
            ProposedPrice = 45000m,
            RoutedDistanceKm = 115m,
            ProposedEtaMinutes = 180,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var adminToken = MintToken(adminUserId, UserRole.Admin);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{assignmentId}/approve")
        {
            Content = JsonContent.Create(new ApproveAssignmentDto
            {
                VehicleId = vehicleId,
                DriverId = driverId,
                Notes = "Approved by Admin on console."
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        if (res.StatusCode == HttpStatusCode.Forbidden) return;

        var body = await res.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(assignmentId, body.AssignmentId);
        Assert.Equal("Accepted", body.Status);
        Assert.NotNull(body.TripId);

        // Verify DB State
        using var checkScope = _factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedAssignment = await checkDb.Assignments
            .Include(a => a.Trip)
            .Include(a => a.Load)
            .FirstAsync(a => a.AssignmentId == assignmentId);

        Assert.Equal(AssignmentStatus.Accepted, updatedAssignment.Status);
        Assert.NotNull(updatedAssignment.Trip);
        Assert.Equal(TripStatus.Assigned, updatedAssignment.Trip.Status);
        Assert.Equal(vehicleId, updatedAssignment.Trip.VehicleId);
        Assert.Equal(driverId, updatedAssignment.Trip.DriverId);

        Assert.Equal(LoadStatus.Matched, updatedAssignment.Load.Status);

        var tripEvent = await checkDb.TripEvents.FirstOrDefaultAsync(e => e.TripId == updatedAssignment.Trip.TripId);
        Assert.NotNull(tripEvent);
        Assert.Equal(TripStatus.Assigned, tripEvent.ToStatus);
        Assert.Equal("Approved by Admin on console.", tripEvent.Notes);

        var decision = await checkDb.ApprovalDecisions.FirstOrDefaultAsync(d => d.WorkflowRunId == workflowRunId);
        Assert.NotNull(decision);
        Assert.Equal(ApprovalDecisionType.Approve, decision.Decision);
        Assert.Equal(adminUserId, decision.DecidedByUserId);

        var run = await checkDb.AgentWorkflowRuns.FirstAsync(r => r.WorkflowRunId == workflowRunId);
        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
    }

    [Fact]
    public async Task Approve_AutoAssignsVehicleAndDriver_WhenNoneProvided()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var staffUserId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var staffUser = new User
        {
            UserId = staffUserId,
            FullName = "Auto Approve Staff",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffUser);

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Auto Shipper",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Auto Fleet Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Yard 2",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Agencies.Add(agency);

        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agencyId,
            RegistrationNo = $"WP-AT-{Guid.NewGuid():N}"[..10],
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000,
            VolumeM3 = 20,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Vehicles.Add(vehicle);

        var driverUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Auto Driver",
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
            LicenceNo = "LIC-AUTO-1",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Drivers.Add(driver);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-AT-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Auto Load",
            WeightKg = 2000,
            VolumeM3 = 10,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.0m,
            DropoffLng = 80.0m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(1),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(5),
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = workflowRunId,
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Auto match",
            Status = WorkflowRunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRunId,
            ProposedPrice = 30000m,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var staffToken = MintToken(staffUserId, UserRole.AgencyStaff);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{assignmentId}/approve")
        {
            Content = JsonContent.Create(new ApproveAssignmentDto())
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Accepted", body.Status);
        Assert.NotNull(body.TripId);
    }

    [Fact]
    public async Task Approve_ReturnsConflict_WhenAssignmentAlreadyDeclined()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var staffUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var staffUser = new User
        {
            UserId = staffUserId,
            FullName = "Declined-Conflict Staff",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffUser);

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Declined Shipper",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Declined Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Yard 3",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Agencies.Add(agency);

        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-DEC-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Declined Cargo",
            WeightKg = 1000,
            VolumeM3 = 5,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.0m,
            DropoffLng = 80.0m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(1),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(5),
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = workflowRunId,
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match",
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRunId,
            ProposedPrice = 20000m,
            Status = AssignmentStatus.Declined,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var staffToken = MintToken(staffUserId, UserRole.AgencyStaff);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{assignmentId}/approve")
        {
            Content = JsonContent.Create(new ApproveAssignmentDto())
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Approve_ReturnsNotFound_WhenAssignmentDoesNotExist()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();

        db.Agencies.Add(new Agency
        {
            AgencyId = agencyId,
            Name = "Not Found Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Yard 5",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.Users.Add(new User
        {
            UserId = staffUserId,
            FullName = "Not Found Staff",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var staffToken = MintToken(staffUserId, UserRole.AgencyStaff);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{Guid.NewGuid()}/approve")
        {
            Content = JsonContent.Create(new ApproveAssignmentDto())
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task AdminAgentWorkflows_Approve_ReturnsForbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var adminUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();

        var adminUser = new User
        {
            UserId = adminUserId,
            FullName = "Admin Operator",
            Email = $"admin-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(adminUser);

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Workflow Shipper",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Candidate Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Yard 4",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Agencies.Add(agency);

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agencyId,
            RegistrationNo = $"WP-CD-{Guid.NewGuid():N}"[..10],
            VehicleType = VehicleType.Lorry,
            CapacityKg = 8000,
            VolumeM3 = 35,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Vehicles.Add(vehicle);

        var driverUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Candidate Driver",
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
            LicenceNo = "LIC-CD-1",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Drivers.Add(driver);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-WF-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Workflow Load",
            WeightKg = 4000,
            VolumeM3 = 18,
            PickupAddress = "Port Site",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Jaffna Site",
            DropoffLat = 9.6m,
            DropoffLng = 80.0m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(2),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(8),
            EstimatedPrice = 85000m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = workflowRunId,
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "AI Match candidate ranking",
            Status = WorkflowRunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var candidate = new MatchCandidate
        {
            MatchCandidateId = Guid.NewGuid(),
            WorkflowRunId = workflowRunId,
            AgencyId = agencyId,
            Rank = 1,
            EligibilityScore = 0.95m,
            Eligible = true,
            EvaluatedAt = DateTimeOffset.UtcNow
        };
        db.MatchCandidates.Add(candidate);
        await db.SaveChangesAsync();

        var adminToken = MintToken(adminUserId, UserRole.Admin);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/agent-workflows/{workflowRunId}/approve")
        {
            Content = JsonContent.Create(new ApproveWorkflowRunDto
            {
                Notes = "Approved from Agent Workflow console."
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        if (res.StatusCode == HttpStatusCode.Forbidden) return;

        var body = await res.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Accepted", body.Status);
        Assert.Equal(agencyId, body.AgencyId);
        Assert.NotNull(body.TripId);

        // Verify DB
        using var checkScope = _factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var run = await checkDb.AgentWorkflowRuns.FirstAsync(r => r.WorkflowRunId == workflowRunId);
        Assert.Equal(WorkflowRunStatus.Completed, run.Status);

        var decision = await checkDb.ApprovalDecisions.FirstAsync(d => d.WorkflowRunId == workflowRunId);
        Assert.Equal(ApprovalDecisionType.Approve, decision.Decision);
        Assert.Equal("Approved from Agent Workflow console.", decision.Reason);
    }

    [Fact]
    public async Task Accept_ReturnsOk_AndCreatesTripAndAssignmentResponse_WhenProposed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Accept Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "10 Yard Way",
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
            FullName = "Accept Staff",
            Email = $"accept-staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffUser);

        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Accept Shipper",
            Email = $"accept-shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-ACC-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Accept Test Cargo",
            WeightKg = 2500,
            VolumeM3 = 10,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.1m,
            DropoffLng = 80.1m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(1),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(5),
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var vehicle = new Vehicle
        {
            VehicleId = vehicleId,
            AgencyId = agencyId,
            RegistrationNo = $"WP-ACC-{Guid.NewGuid():N}"[..8],
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000,
            VolumeM3 = 20,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Vehicles.Add(vehicle);

        var driverUser = new User
        {
            UserId = driverUserId,
            FullName = "Accept Driver",
            Email = $"accept-driver-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Driver,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(driverUser);

        var driver = new Driver
        {
            DriverId = driverId,
            AgencyId = agencyId,
            UserId = driverUserId,
            LicenceNo = $"B-{Guid.NewGuid():N}"[..8],
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Drivers.Add(driver);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = workflowRunId,
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match load to agency",
            Status = WorkflowRunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRunId,
            ProposedPrice = 35000m,
            RoutedDistanceKm = 80m,
            ProposedEtaMinutes = 120,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var token = MintToken(staffUserId, UserRole.AgencyStaff);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{loadId}/accept")
        {
            Content = JsonContent.Create(new ApproveAssignmentDto
            {
                VehicleId = vehicleId,
                DriverId = driverId,
                Notes = "Agency accepted proposed job."
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Accepted", body.Status);
        Assert.NotNull(body.TripId);

        // Verify in database
        using var checkScope = _factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedAssignment = await checkDb.Assignments
            .Include(a => a.Response)
            .Include(a => a.Trip)
            .FirstAsync(a => a.AssignmentId == assignmentId);

        Assert.Equal(AssignmentStatus.Accepted, updatedAssignment.Status);
        Assert.NotNull(updatedAssignment.Response);
        Assert.Equal(AssignmentResponseType.Accepted, updatedAssignment.Response.Response);
        Assert.Equal(staffUserId, updatedAssignment.Response.RespondedByUserId);
        Assert.Null(updatedAssignment.Response.DeclineReason);

        Assert.NotNull(updatedAssignment.Trip);
        Assert.Equal(TripStatus.Assigned, updatedAssignment.Trip.Status);
        Assert.Equal(vehicleId, updatedAssignment.Trip.VehicleId);
        Assert.Equal(driverId, updatedAssignment.Trip.DriverId);

        var updatedLoad = await checkDb.Loads.FirstAsync(l => l.LoadId == loadId);
        Assert.Equal(LoadStatus.Matched, updatedLoad.Status);

        var updatedRun = await checkDb.AgentWorkflowRuns.FirstAsync(r => r.WorkflowRunId == workflowRunId);
        Assert.Equal(WorkflowRunStatus.Completed, updatedRun.Status);
    }

    [Fact]
    public async Task Accept_ReturnsConflict_WhenAssignmentAlreadyDeclined()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Declined Conflict Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "12 Yard Way",
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
            FullName = "Declined Staff",
            Email = $"dec-staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffUser);

        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Conflict Shipper",
            Email = $"conf-shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-CNF-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Conflict Cargo",
            WeightKg = 1500,
            VolumeM3 = 6,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.1m,
            DropoffLng = 80.1m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match load",
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 28000m,
            Status = AssignmentStatus.Declined,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var token = MintToken(staffUserId, UserRole.AgencyStaff);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{loadId}/accept");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Accept_ReturnsForbidden_WhenCallerIsStaffOfDifferentAgency()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyAId = Guid.NewGuid();
        var agencyBId = Guid.NewGuid();
        var staffBUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        db.Agencies.Add(new Agency
        {
            AgencyId = agencyAId,
            Name = "Agency A",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Yard A",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Agencies.Add(new Agency
        {
            AgencyId = agencyBId,
            Name = "Agency B",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Yard B",
            YardLat = 7.0m,
            YardLng = 79.9m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var staffBUser = new User
        {
            UserId = staffBUserId,
            FullName = "Staff B",
            Email = $"staff-b-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffBUser);

        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyBId,
            UserId = staffBUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Shipper",
            Email = $"shipper-ab-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-AB-{Guid.NewGuid():N}"[..12],
            CargoDescription = "AB Cargo",
            WeightKg = 1000,
            VolumeM3 = 5,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.1m,
            DropoffLng = 80.1m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match load",
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyAId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 25000m,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var tokenB = MintToken(staffBUserId, UserRole.AgencyStaff);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{loadId}/accept");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Decline_ReturnsOk_AndPersistsAssignmentResponse_WhenProposed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Decline Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "20 Decline Way",
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
            FullName = "Decline Staff",
            Email = $"dec-staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffUser);

        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Shipper To Notify",
            Email = $"shipper-notify-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-DEC-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Decline Cargo",
            WeightKg = 2000,
            VolumeM3 = 8,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.1m,
            DropoffLng = 80.1m,
            // Matched, not Posted: by the time an agency can decline a proposal, ConfirmMatchAsync
            // has already moved the load to Matched (the Shipper confirmed an agency before that
            // agency ever saw the proposal) - seeding Posted here would mask the exact Matched->Posted
            // revert this test now verifies.
            Status = LoadStatus.Matched,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = workflowRunId,
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match load",
            Status = WorkflowRunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRunId,
            ProposedPrice = 40000m,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var token = MintToken(staffUserId, UserRole.AgencyStaff);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{loadId}/decline")
        {
            Content = JsonContent.Create(new DeclineAssignmentDto
            {
                Reason = "No available heavy truck for this route."
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Declined", body.Status);
        Assert.Equal("No available heavy truck for this route.", body.DeclineReason);

        // Verify database
        using var checkScope = _factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedAssignment = await checkDb.Assignments
            .Include(a => a.Response)
            .Include(a => a.Trip)
            .FirstAsync(a => a.AssignmentId == assignmentId);

        Assert.Equal(AssignmentStatus.Declined, updatedAssignment.Status);
        Assert.NotNull(updatedAssignment.Response);
        Assert.Equal(AssignmentResponseType.Declined, updatedAssignment.Response.Response);
        Assert.Equal("No available heavy truck for this route.", updatedAssignment.Response.DeclineReason);
        Assert.Equal(staffUserId, updatedAssignment.Response.RespondedByUserId);
        Assert.Null(updatedAssignment.Trip);

        var updatedLoad = await checkDb.Loads.FirstAsync(l => l.LoadId == loadId);
        Assert.Equal(LoadStatus.Posted, updatedLoad.Status);
    }

    /// <summary>
    /// Regression test: once an agency declines a proposed match, the Shipper's match-recommendation
    /// view must stop reporting the load as finalized. Before this fix, Load.Status stayed stuck on
    /// Matched (ConfirmMatchAsync sets it before the agency ever responds, and Decline never reverted
    /// it) and GetMatchRecommendationAsync's ExistingAssignment lookup ignored status entirely, so it
    /// kept returning the just-declined assignment - together these made the React console's
    /// "isFinalized" check permanently true, hiding the live decision UI for the brand new
    /// AwaitingApproval retry run a Shipper needs to act on next.
    /// </summary>
    [Fact]
    public async Task Decline_ThenGetMatchRecommendation_NoLongerReportsLoadAsFinalized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        db.Agencies.Add(new Agency
        {
            AgencyId = agencyId,
            Name = "Post-Decline Check Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "30 Decline Way",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Users.Add(new User
        {
            UserId = staffUserId,
            FullName = "Post-Decline Staff",
            Email = $"post-dec-staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Users.Add(new User
        {
            UserId = shipperUserId,
            FullName = "Post-Decline Shipper",
            Email = $"post-dec-shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Loads.Add(new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-PDC-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Post-Decline Cargo",
            WeightKg = 2000,
            VolumeM3 = 8,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.1m,
            DropoffLng = 80.1m,
            // Matched, mirroring ConfirmMatchAsync having already run before the agency responds.
            Status = LoadStatus.Matched,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.AgentWorkflowRuns.Add(new AgentWorkflowRun
        {
            WorkflowRunId = workflowRunId,
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match load",
            Status = WorkflowRunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRunId,
            ProposedPrice = 40000m,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var agencyToken = MintToken(staffUserId, UserRole.AgencyStaff);
        var declineReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{loadId}/decline")
        {
            Content = JsonContent.Create(new DeclineAssignmentDto { Reason = "No available heavy truck for this route." })
        };
        declineReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agencyToken);
        var declineRes = await _client.SendAsync(declineReq);
        Assert.Equal(HttpStatusCode.OK, declineRes.StatusCode);

        var shipperToken = MintToken(shipperUserId, UserRole.Shipper);
        var matchReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/loads/{loadId}/match");
        matchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var matchRes = await _client.SendAsync(matchReq);
        Assert.Equal(HttpStatusCode.OK, matchRes.StatusCode);

        var match = await matchRes.Content.ReadFromJsonAsync<LoadMatchRecommendationDto>();
        Assert.NotNull(match);
        Assert.Equal("Posted", match.LoadStatus);
        Assert.Null(match.ExistingAssignment);
    }

    [Fact]
    public async Task Decline_ThirdConsecutiveAttempt_MarksWorkflowRunFailed_NoFourthAutomaticAttempt()
    {
        // ADR-018 retry cascade: attempts 1-2 leave the load Posted for another match attempt;
        // attempt 3 (the cap) must record a safe, terminal failure - never a silent infinite loop,
        // never a 4th automatic attempt (plans/06-testing-and-verification-plan.md §4).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var shipperUserId = Guid.NewGuid();
        db.Users.Add(new User
        {
            UserId = shipperUserId,
            FullName = "Cascade Shipper",
            Email = $"cascade-shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var loadId = Guid.NewGuid();
        db.Loads.Add(new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-CAS-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Cascade Cargo",
            WeightKg = 2000,
            VolumeM3 = 8,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.1m,
            DropoffLng = 80.1m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var workflowRunIds = new Guid[3];
        var assignmentIds = new Guid[3];

        for (var i = 0; i < 3; i++)
        {
            var attemptNo = i + 1;
            var agencyId = Guid.NewGuid();
            var staffUserId = Guid.NewGuid();
            workflowRunIds[i] = Guid.NewGuid();
            assignmentIds[i] = Guid.NewGuid();

            db.Agencies.Add(new Agency
            {
                AgencyId = agencyId,
                Name = $"Cascade Agency Attempt {attemptNo}",
                BusinessRegNo = $"BR-{Guid.NewGuid():N}",
                YardAddress = "Cascade Yard",
                YardLat = 6.9m,
                YardLng = 79.8m,
                Status = AgencyStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            var staffUser = new User
            {
                UserId = staffUserId,
                FullName = $"Cascade Staff {attemptNo}",
                Email = $"cascade-staff-{Guid.NewGuid():N}@example.com",
                PasswordHash = "hash",
                Role = UserRole.AgencyStaff,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(staffUser);
            db.AgencyStaff.Add(new AgencyStaff
            {
                AgencyId = agencyId,
                UserId = staffUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            db.AgentWorkflowRuns.Add(new AgentWorkflowRun
            {
                WorkflowRunId = workflowRunIds[i],
                LoadId = loadId,
                TriggeredByUserId = shipperUserId,
                AttemptNo = attemptNo,
                Objective = $"Match load, attempt {attemptNo}",
                Status = WorkflowRunStatus.AwaitingApproval,
                StartedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            db.Assignments.Add(new Assignment
            {
                AssignmentId = assignmentIds[i],
                LoadId = loadId,
                AgencyId = agencyId,
                WorkflowRunId = workflowRunIds[i],
                ProposedPrice = 40000m,
                Status = AssignmentStatus.Proposed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();

            var token = MintToken(staffUserId, UserRole.AgencyStaff);
            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{assignmentIds[i]}/decline")
            {
                Content = JsonContent.Create(new DeclineAssignmentDto { Reason = $"Declining attempt {attemptNo}" })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        using var checkScope = _factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var firstRun = await checkDb.AgentWorkflowRuns.FirstAsync(r => r.WorkflowRunId == workflowRunIds[0]);
        var secondRun = await checkDb.AgentWorkflowRuns.FirstAsync(r => r.WorkflowRunId == workflowRunIds[1]);
        var thirdRun = await checkDb.AgentWorkflowRuns.FirstAsync(r => r.WorkflowRunId == workflowRunIds[2]);

        // Attempts 1 and 2 are below the cap - no terminal failure recorded for them.
        Assert.NotEqual(WorkflowRunStatus.Failed, firstRun.Status);
        Assert.NotEqual(WorkflowRunStatus.Failed, secondRun.Status);

        // Attempt 3 (the cap) must be a real, recorded, terminal failure.
        Assert.Equal(WorkflowRunStatus.Failed, thirdRun.Status);
        Assert.NotNull(thirdRun.CompletedAt);

        var allAssignments = await checkDb.Assignments.Where(a => a.LoadId == loadId).ToListAsync();
        Assert.Equal(3, allAssignments.Count);
        Assert.All(allAssignments, a => Assert.Equal(AssignmentStatus.Declined, a.Status));
    }

    [Fact]
    public async Task Decline_ReturnsConflict_WhenAssignmentAlreadyDeclined()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Double Decline Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "21 Decline Way",
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
            FullName = "Double Decline Staff",
            Email = $"dbl-staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffUser);

        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Double Shipper",
            Email = $"dbl-shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-DBL-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Double Cargo",
            WeightKg = 1000,
            VolumeM3 = 4,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.1m,
            DropoffLng = 80.1m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match load",
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 25000m,
            Status = AssignmentStatus.Declined,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var token = MintToken(staffUserId, UserRole.AgencyStaff);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{loadId}/decline")
        {
            Content = JsonContent.Create(new DeclineAssignmentDto { Reason = "Declining again" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Decline_ReturnsConflict_WhenAssignmentAlreadyAccepted()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencyId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Accepted Decline Conflict Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "22 Decline Way",
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
            FullName = "Accepted Decline Staff",
            Email = $"acc-dec-staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(staffUser);

        db.AgencyStaff.Add(new AgencyStaff
        {
            AgencyId = agencyId,
            UserId = staffUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var shipperUser = new User
        {
            UserId = shipperUserId,
            FullName = "Accepted Shipper",
            Email = $"acc-shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(shipperUser);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-ACCD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Accepted Cargo",
            WeightKg = 1000,
            VolumeM3 = 4,
            PickupAddress = "Site A",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffAddress = "Site B",
            DropoffLat = 7.1m,
            DropoffLng = 80.1m,
            Status = LoadStatus.Matched,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = loadId,
            TriggeredByUserId = shipperUserId,
            AttemptNo = 1,
            Objective = "Match load",
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AgentWorkflowRuns.Add(workflowRun);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 25000m,
            Status = AssignmentStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var token = MintToken(staffUserId, UserRole.AgencyStaff);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{loadId}/decline")
        {
            Content = JsonContent.Create(new DeclineAssignmentDto { Reason = "Attempt to decline accepted load" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

}
