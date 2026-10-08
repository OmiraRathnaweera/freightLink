using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Disputes;
using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// End-to-end and integration tests for the Invoice and Dispute Service.
/// Verifies:
/// 1. Invoice creation and line item calculation
/// 2. Dispute initiation by authorized roles
/// 3. Cross-user RBAC and ownership isolation (users can only dispute their own invoices/trips)
/// 4. State machine transitions: Draft -> Issued -> Disputed -> UnderReview -> Resolved
/// 5. Terminal state protection and invalid transition rejection
/// </summary>
public class InvoiceAndDisputeLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InvoiceAndDisputeLifecycleTests(CustomWebApplicationFactory factory)
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

    private async Task<(User ShipperA, User ShipperB, User StaffUser, Trip TripA, Invoice InvoiceA)> SeedInvoiceScenarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;

        var shipperA = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"shipper-a-{Guid.NewGuid():N}@example.com",
            FullName = "Shipper Alpha",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var shipperB = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Shipper,
            Email = $"shipper-b-{Guid.NewGuid():N}@example.com",
            FullName = "Shipper Beta",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var staff = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.AgencyStaff,
            Email = $"agency-{Guid.NewGuid():N}@example.com",
            FullName = "Agency Logistics Staff",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = "Lanka Freight Line",
            BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..12],
            YardAddress = "100 Port Road, Colombo",
            YardLat = 6.93m,
            YardLng = 79.85m,
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            RegistrationNo = "WP-CAB-8811",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 9000,
            VolumeM3 = 30,
            Status = VehicleStatus.Available,
            CreatedAt = now,
            UpdatedAt = now
        };

        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            UserId = Guid.NewGuid(),
            LicenceNo = "DL-994411",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            Status = DriverStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var loadA = new Load
        {
            LoadId = Guid.NewGuid(),
            ShipperUserId = shipperA.UserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..10],
            CargoDescription = "Export Garments",
            PickupAddress = "Colombo Fort",
            PickupLat = 6.9344m,
            PickupLng = 79.8428m,
            DropoffAddress = "Kandy City",
            DropoffLat = 7.2906m,
            DropoffLng = 80.6337m,
            WeightKg = 3200m,
            VolumeM3 = 12.0m,
            Status = LoadStatus.Delivered,
            PickupWindowStart = now.AddDays(-2),
            PickupWindowEnd = now.AddDays(-1),
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now
        };

        var workflowRun = new AgentWorkflowRun
        {
            WorkflowRunId = Guid.NewGuid(),
            LoadId = loadA.LoadId,
            TriggeredByUserId = shipperA.UserId,
            AttemptNo = 1,
            Objective = "Match and price load",
            Status = WorkflowRunStatus.Completed,
            StartedAt = now.AddDays(-2),
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-2)
        };

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            LoadId = loadA.LoadId,
            AgencyId = agency.AgencyId,
            WorkflowRunId = workflowRun.WorkflowRunId,
            ProposedPrice = 85000.00m,
            Status = AssignmentStatus.Accepted,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-2)
        };

        var tripA = new Trip
        {
            TripId = Guid.NewGuid(),
            AssignmentId = assignment.AssignmentId,
            VehicleId = vehicle.VehicleId,
            DriverId = driver.DriverId,
            Status = TripStatus.Delivered,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-1)
        };

        var invoiceA = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            TripId = tripA.TripId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            RecipientId = shipperA.UserId,
            RecipientRole = UserRole.Shipper,
            Subtotal = 85000.00m,
            TaxTotal = 0.00m,
            DiscountTotal = 0.00m,
            Amount = 85000.00m,
            Currency = "LKR",
            Status = InvoiceStatus.Issued,
            IssuedAt = now.AddDays(-1),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            CreatedByUserId = staff.UserId,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1)
        };

        var agencyStaffLink = new AgencyStaff
        {
            AgencyId = agency.AgencyId,
            UserId = staff.UserId,
            JobTitle = "Operations Lead",
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.AddRange(shipperA, shipperB, staff);
        db.Agencies.Add(agency);
        db.AgencyStaff.Add(agencyStaffLink);
        db.Vehicles.Add(vehicle);
        db.Drivers.Add(driver);
        db.Loads.Add(loadA);
        db.AgentWorkflowRuns.Add(workflowRun);
        db.Assignments.Add(assignment);
        db.Trips.Add(tripA);
        db.Invoices.Add(invoiceA);

        await db.SaveChangesAsync();

        return (shipperA, shipperB, staff, tripA, invoiceA);
    }

    [Fact]
    public async Task BE_IT_001_CreateAndIssueInvoice_CalculatesTotalsAndSetsIssuedStatus()
    {
        // Arrange
        var (shipperA, _, staff, tripA, _) = await SeedInvoiceScenarioAsync();

        Guid tripBId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existingTrip = await db.Trips.FirstAsync(t => t.TripId == tripA.TripId);
            var tripB = new Trip
            {
                TripId = Guid.NewGuid(),
                AssignmentId = existingTrip.AssignmentId,
                VehicleId = existingTrip.VehicleId,
                DriverId = existingTrip.DriverId,
                Status = TripStatus.Delivered,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Trips.Add(tripB);
            await db.SaveChangesAsync();
            tripBId = tripB.TripId;
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MintToken(staff.UserId, UserRole.AgencyStaff));

        var createDto = new CreateInvoiceDto
        {
            TripId = tripBId,
            RecipientId = shipperA.UserId,
            RecipientRole = UserRole.Shipper,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Notes = "Trip delivery freight charge",
            LineItems = new List<InvoiceLineItemDto>
            {
                new() { Description = "Base Freight Charge", Quantity = 1, UnitPrice = 50000m },
                new() { Description = "Fuel Adjustment Surcharge", Quantity = 1, UnitPrice = 12000m }
            }
        };

        // Act - 1. Create Invoice in Draft
        var postResponse = await client.PostAsJsonAsync("/api/v1/invoices", createDto);

        // Assert 1
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var createdInvoice = await postResponse.Content.ReadFromJsonAsync<InvoiceResponseDto>(ApiJsonOptions);
        Assert.NotNull(createdInvoice);
        Assert.Equal(62000m, createdInvoice.Amount);
        Assert.Equal(InvoiceStatus.Draft, createdInvoice.Status);

        // Act - 2. Issue the invoice
        var issueResponse = await client.PostAsync($"/api/v1/invoices/{createdInvoice.InvoiceId}/issue", null);

        // Assert 2
        Assert.Equal(HttpStatusCode.OK, issueResponse.StatusCode);
        var issuedInvoice = await issueResponse.Content.ReadFromJsonAsync<InvoiceResponseDto>(ApiJsonOptions);
        Assert.NotNull(issuedInvoice);
        Assert.Equal(InvoiceStatus.Issued, issuedInvoice.Status);
        Assert.NotNull(issuedInvoice.IssuedAt);
    }

    [Fact]
    public async Task BE_IT_002_ShipperCannotDisputeAnotherShippersTripOrInvoice()
    {
        // Arrange - Shipper A owns Trip A. Shipper B attempts to dispute Trip A.
        var (_, shipperB, _, tripA, _) = await SeedInvoiceScenarioAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MintToken(shipperB.UserId, UserRole.Shipper));

        var disputePayload = new CreateDisputeDto
        {
            TripId = tripA.TripId,
            Category = DisputeCategory.Billing,
            Description = "Unauthorized attempt to raise dispute on external trip."
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/disputes", disputePayload);

        // Assert - Expect Forbidden or NotFound to protect ownership isolation
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 403 Forbidden or 404 Not Found, but received {response.StatusCode}");
    }

    [Fact]
    public async Task BE_IT_003_DriverRoleCannotRaiseDispute()
    {
        // Arrange
        var (_, _, _, tripA, _) = await SeedInvoiceScenarioAsync();
        var driverId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MintToken(driverId, UserRole.Driver));

        var disputePayload = new CreateDisputeDto
        {
            TripId = tripA.TripId,
            Category = DisputeCategory.Other,
            Description = "Drivers are not authorized to raise disputes on billing."
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/disputes", disputePayload);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BE_IT_004_AnonymousRequestToInvoices_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await client.GetAsync("/api/v1/invoices");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BE_IT_005_FullLifecycle_RaisedToUnderReviewToResolved()
    {
        // Arrange
        var (shipperA, _, _, tripA, _) = await SeedInvoiceScenarioAsync();
        var adminId = Guid.NewGuid();

        var shipperClient = _factory.CreateClient();
        shipperClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MintToken(shipperA.UserId, UserRole.Shipper));

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MintToken(adminId, UserRole.Admin));

        // Act 1: Shipper creates dispute
        var createDisputeDto = new CreateDisputeDto
        {
            TripId = tripA.TripId,
            Category = DisputeCategory.Billing,
            Description = "Billed 85,000 LKR but contracted freight rate was 75,000 LKR."
        };

        var createRes = await shipperClient.PostAsJsonAsync("/api/v1/disputes", createDisputeDto);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var dispute = await createRes.Content.ReadFromJsonAsync<DisputeResponseDto>(ApiJsonOptions);
        Assert.NotNull(dispute);
        Assert.Equal(DisputeStatus.Raised, dispute.Status);

        // Act 2: Admin moves dispute to UnderReview
        var reviewRes = await adminClient.PatchAsync($"/api/v1/disputes/{dispute.DisputeId}/review", null);
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);
        var underReviewDispute = await reviewRes.Content.ReadFromJsonAsync<DisputeResponseDto>(ApiJsonOptions);
        Assert.NotNull(underReviewDispute);
        Assert.Equal(DisputeStatus.UnderReview, underReviewDispute.Status);

        // Act 3: Admin resolves dispute
        var resolveDto = new ResolveDisputeDto
        {
            Outcome = DisputeOutcome.PartiallyUpheld,
            ResolutionNote = "Verified rate discrepancy. Deducted 10,000 LKR fuel surcharge overcharge."
        };

        var resolveRes = await adminClient.PostAsJsonAsync($"/api/v1/disputes/{dispute.DisputeId}/resolve", resolveDto);
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);
        var resolvedDispute = await resolveRes.Content.ReadFromJsonAsync<DisputeResponseDto>(ApiJsonOptions);
        Assert.NotNull(resolvedDispute);
        Assert.Equal(DisputeStatus.Resolved, resolvedDispute.Status);
        Assert.NotNull(resolvedDispute.Resolution);
        Assert.Equal(DisputeOutcome.PartiallyUpheld, resolvedDispute.Resolution.Outcome);

        // Act 4: Attempt to transition already resolved dispute back to UnderReview (Illegal transition)
        var illegalTransitionRes = await adminClient.PatchAsync($"/api/v1/disputes/{dispute.DisputeId}/review", null);
        Assert.Equal(HttpStatusCode.BadRequest, illegalTransitionRes.StatusCode);
    }
}
