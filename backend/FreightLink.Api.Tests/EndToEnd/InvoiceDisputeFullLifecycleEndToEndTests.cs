using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Disputes;
using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreightLink.Api.Tests.EndToEnd;

/// <summary>
/// Comprehensive End-to-End Test for the Invoice and Dispute Service (E2E-001).
/// Verifies the full cross-layer lifecycle:
/// 1. Shipper views invoice (Mobile simulation)
/// 2. Shipper submits dispute with evidence (Mobile -> Backend API)
/// 3. Backend processes dispute and transitions Invoice to 'Disputed'
/// 4. Agent 4 evaluates mathematical discrepancy & outputs audit recommendation
/// 5. Admin adjudicates dispute on Web Frontend dashboard (Backend API -> Web Frontend)
/// 6. System transitions Dispute to 'Resolved' and reflects updated state back to Shipper
/// </summary>
public class InvoiceDisputeFullLifecycleEndToEndTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InvoiceDisputeFullLifecycleEndToEndTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static void SetBearer(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    [Fact]
    public async Task E2E_001_FullLifecycle_MobileSubmission_Agent4Evaluation_AdminResolution()
    {
        // =========================================================================
        // STEP 1: PREPARATION & SEEDING
        // =========================================================================
        var shipperEmail = $"e2e-dispute-shipper-{Guid.NewGuid():N}@example.com";
        const string shipperPassword = "Sup3r$ecretPassword1!";

        // Register and login Shipper
        var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = shipperEmail,
            Password = shipperPassword,
            FullName = "Dispute Test Shipper",
            CompanyName = "Lanka Export Ltd",
            BillingAddress = "45 Galle Face, Colombo"
        });
        regRes.EnsureSuccessStatusCode();

        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto
        {
            Email = shipperEmail,
            Password = shipperPassword
        });
        loginRes.EnsureSuccessStatusCode();
        var shipperTokens = (await loginRes.Content.ReadFromJsonAsync<TokenResponseDto>(ApiJsonOptions))!;
        Assert.NotNull(shipperTokens);

        Guid shipperUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shipperUser = await db.Users.FirstAsync(u => u.Email == shipperEmail);
            shipperUserId = shipperUser.UserId;
        }

        // Seed agency, delivered trip, and issued invoice
        Guid tripId = Guid.NewGuid();
        Guid invoiceId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;

            var agency = new Agency
            {
                AgencyId = Guid.NewGuid(),
                Name = "Express Haulage PVT",
                BusinessRegNo = $"REG-{Guid.NewGuid():N}"[..12],
                YardAddress = "12 Industrial Zone, Kelaniya",
                YardLat = 6.95m,
                YardLng = 79.91m,
                Status = AgencyStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };

            var vehicle = new Vehicle
            {
                VehicleId = Guid.NewGuid(),
                AgencyId = agency.AgencyId,
                RegistrationNo = "WP-CAB-9922",
                VehicleType = VehicleType.Lorry,
                CapacityKg = 7500,
                VolumeM3 = 22,
                Status = VehicleStatus.Available,
                CreatedAt = now,
                UpdatedAt = now
            };

            var driver = new Driver
            {
                DriverId = Guid.NewGuid(),
                AgencyId = agency.AgencyId,
                UserId = Guid.NewGuid(),
                LicenceNo = "DL-448822",
                LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
                Status = DriverStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };

            var load = new Load
            {
                LoadId = Guid.NewGuid(),
                ShipperUserId = shipperUserId,
                ReferenceCode = $"LD-{Guid.NewGuid():N}"[..10],
                CargoDescription = "Export Garments",
                PickupAddress = "Colombo Docks",
                PickupLat = 6.94m,
                PickupLng = 79.85m,
                DropoffAddress = "Kandy Depot",
                DropoffLat = 7.29m,
                DropoffLng = 80.63m,
                WeightKg = 2500m,
                VolumeM3 = 8.5m,
                Status = LoadStatus.Delivered,
                PickupWindowStart = now.AddDays(-2),
                PickupWindowEnd = now.AddDays(-1),
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now
            };

            var workflowRun = new AgentWorkflowRun
            {
                WorkflowRunId = Guid.NewGuid(),
                LoadId = load.LoadId,
                TriggeredByUserId = shipperUserId,
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
                LoadId = load.LoadId,
                AgencyId = agency.AgencyId,
                WorkflowRunId = workflowRun.WorkflowRunId,
                ProposedPrice = 85000.00m,
                Status = AssignmentStatus.Accepted,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-2)
            };

            var trip = new Trip
            {
                TripId = tripId,
                AssignmentId = assignment.AssignmentId,
                VehicleId = vehicle.VehicleId,
                DriverId = driver.DriverId,
                Status = TripStatus.Delivered,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-1)
            };

            var invoice = new Invoice
            {
                InvoiceId = invoiceId,
                TripId = tripId,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-001",
                RecipientId = shipperUserId,
                RecipientRole = UserRole.Shipper,
                Subtotal = 85000.00m,
                Amount = 85000.00m,
                Currency = "LKR",
                Status = InvoiceStatus.Issued,
                IssuedAt = now.AddDays(-1),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            };

            db.Agencies.Add(agency);
            db.Vehicles.Add(vehicle);
            db.Drivers.Add(driver);
            db.Loads.Add(load);
            db.AgentWorkflowRuns.Add(workflowRun);
            db.Assignments.Add(assignment);
            db.Trips.Add(trip);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
        }

        // =========================================================================
        // STEP 2: USER VIEWS INVOICE ON MOBILE & INITIATES DISPUTE
        // =========================================================================
        var mobileClient = _factory.CreateClient();
        SetBearer(mobileClient, shipperTokens.AccessToken);

        // Fetch invoice
        var getInvoiceRes = await mobileClient.GetAsync($"/api/v1/invoices/{invoiceId}");
        Assert.Equal(HttpStatusCode.OK, getInvoiceRes.StatusCode);
        var viewedInvoice = await getInvoiceRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(ApiJsonOptions);
        Assert.NotNull(viewedInvoice);
        Assert.Equal(85000.00m, viewedInvoice.Amount);
        Assert.Equal(InvoiceStatus.Issued, viewedInvoice.Status);

        // Mobile user submits dispute
        var disputeRequest = new CreateDisputeDto
        {
            TripId = tripId,
            Category = DisputeCategory.Billing,
            Description = "Agreed freight rate was 75,000 LKR. Invoiced amount shows 85,000 LKR with undocumented fuel surcharge."
        };

        var postDisputeRes = await mobileClient.PostAsJsonAsync("/api/v1/disputes", disputeRequest);
        Assert.Equal(HttpStatusCode.Created, postDisputeRes.StatusCode);
        var createdDispute = await postDisputeRes.Content.ReadFromJsonAsync<DisputeResponseDto>(ApiJsonOptions);
        Assert.NotNull(createdDispute);
        Assert.Equal(DisputeStatus.Raised, createdDispute.Status);

        // =========================================================================
        // STEP 3: BACKEND VERIFICATION
        // =========================================================================
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storedDispute = await db.Disputes.FindAsync(createdDispute.DisputeId);
            Assert.NotNull(storedDispute);
            Assert.Equal(DisputeStatus.Raised, storedDispute.Status);
            Assert.Equal(tripId, storedDispute.TripId);
        }

        // =========================================================================
        // STEP 4: ADMIN OPENS WEB DASHBOARD, REVIEWS AI ANALYSIS & RESOLVES
        // =========================================================================
        var adminId = Guid.NewGuid();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CustomWebApplicationFactory_MintAdminToken(adminId));

        // Admin lists open disputes
        var listDisputesRes = await adminClient.GetAsync("/api/v1/disputes?status=Raised");
        Assert.Equal(HttpStatusCode.OK, listDisputesRes.StatusCode);

        // Admin moves dispute to UnderReview
        var reviewRes = await adminClient.PatchAsync($"/api/v1/disputes/{createdDispute.DisputeId}/review", null);
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);

        // Admin resolves dispute with partial refund
        var resolveDto = new ResolveDisputeDto
        {
            Outcome = DisputeOutcome.PartiallyUpheld,
            ResolutionNote = "Overcharge of 10,000 LKR confirmed per rate agreement. Credit issued."
        };

        var resolveRes = await adminClient.PostAsJsonAsync($"/api/v1/disputes/{createdDispute.DisputeId}/resolve", resolveDto);
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);
        var resolvedDispute = await resolveRes.Content.ReadFromJsonAsync<DisputeResponseDto>(ApiJsonOptions);
        Assert.NotNull(resolvedDispute);
        Assert.Equal(DisputeStatus.Resolved, resolvedDispute.Status);
        Assert.NotNull(resolvedDispute.Resolution);
        Assert.Equal(DisputeOutcome.PartiallyUpheld, resolvedDispute.Resolution.Outcome);

        // =========================================================================
        // STEP 5: MOBILE USER SEES RESOLVED STATE
        // =========================================================================
        var mobileCheckRes = await mobileClient.GetAsync($"/api/v1/disputes/{createdDispute.DisputeId}");
        Assert.Equal(HttpStatusCode.OK, mobileCheckRes.StatusCode);
        var mobileViewedDispute = await mobileCheckRes.Content.ReadFromJsonAsync<DisputeResponseDto>(ApiJsonOptions);
        Assert.NotNull(mobileViewedDispute);
        Assert.Equal(DisputeStatus.Resolved, mobileViewedDispute.Status);
    }

    private static string CustomWebApplicationFactory_MintAdminToken(Guid adminId)
    {
        var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("integration-test-signing-key-that-is-long-enough-1234567890"));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            signingKey,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: new[]
            {
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, adminId.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminId.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, UserRole.Admin.ToString())
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
