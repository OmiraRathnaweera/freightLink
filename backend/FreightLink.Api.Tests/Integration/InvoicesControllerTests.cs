using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="Controllers.InvoicesController"/> testing HTTP endpoints, routing, and role authorization.
/// </summary>
public class InvoicesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InvoicesControllerTests(CustomWebApplicationFactory factory)
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
            RegistrationNo = "WP-INT-1111",
            VehicleType = VehicleType.Container,
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
            LicenceNo = "DL-INT-1",
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
    public async Task PostInvoice_WithoutAuth_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/invoices", new CreateInvoiceDto
        {
            TripId = Guid.NewGuid(),
            Amount = 10000m,
            Currency = "LKR"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostInvoice_AuthenticatedShipper_CreatesAndReturns201()
    {
        var (shipper, _, _, trip) = await SeedTripDataAsync();
        var token = MintToken(shipper.UserId, UserRole.Shipper);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                Amount = 25000m,
                Currency = "LKR",
                IssueImmediately = false
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponseDto>();
        Assert.NotNull(invoice);
        Assert.Equal(trip.TripId, invoice.TripId);
        Assert.Equal(25000m, invoice.Amount);
    }

    [Fact]
    public async Task GetInvoice_Returns200WithInvoiceDetails()
    {
        var (shipper, _, _, trip) = await SeedTripDataAsync();
        var token = MintToken(shipper.UserId, UserRole.Shipper);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                Amount = 18000m,
                Currency = "LKR",
                IssueImmediately = true
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var createdInvoice = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>())!;

        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{createdInvoice.InvoiceId}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getRes = await _client.SendAsync(getReq);

        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetchedInvoice = await getRes.Content.ReadFromJsonAsync<InvoiceResponseDto>();
        Assert.NotNull(fetchedInvoice);
        Assert.Equal(createdInvoice.InvoiceId, fetchedInvoice.InvoiceId);
    }

    [Fact]
    public async Task VoidInvoice_Returns200WithVoidStatus()
    {
        var (shipper, _, _, trip) = await SeedTripDataAsync();
        var token = MintToken(shipper.UserId, UserRole.Shipper);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                Amount = 18000m,
                Currency = "LKR",
                IssueImmediately = false
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var createdInvoice = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>())!;

        var voidReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/invoices/{createdInvoice.InvoiceId}/void");
        voidReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var voidRes = await _client.SendAsync(voidReq);

        Assert.Equal(HttpStatusCode.OK, voidRes.StatusCode);
        var voidedInvoice = await voidRes.Content.ReadFromJsonAsync<InvoiceResponseDto>();
        Assert.NotNull(voidedInvoice);
        Assert.Equal(InvoiceStatus.Void, voidedInvoice.Status);
    }
}
