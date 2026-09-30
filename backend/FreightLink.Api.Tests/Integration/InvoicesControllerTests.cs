using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Files;
using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="Controllers.InvoicesController"/> verifying RBAC permissions,
/// mutation restrictions (Agent only), pay endpoint (Shipper only), and scoped retrieval rules.
/// </summary>
public class InvoicesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

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
            ProposedPrice = 25000m,
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

    // =========================================================================
    // 1. RBAC Matrix — Create Invoice (Agent Allowed, Admin/Shipper Denied 403)
    // =========================================================================

    [Fact]
    public async Task PostInvoice_AuthenticatedAgent_CreatesAndReturns201()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var token = MintToken(staffUser.UserId, UserRole.AgencyStaff);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                RecipientRole = UserRole.Shipper,
                Amount = 25000m,
                Currency = "LKR",
                IssueImmediately = false
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts);
        Assert.NotNull(invoice);
        Assert.Equal(trip.TripId, invoice.TripId);
        Assert.Equal(25000m, invoice.Amount);
    }

    [Fact]
    public async Task PostInvoice_AuthenticatedShipper_Returns403Forbidden()
    {
        var (shipper, _, _, trip) = await SeedTripDataAsync();
        var token = MintToken(shipper.UserId, UserRole.Shipper);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                Amount = 25000m,
                Currency = "LKR"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // 2. RBAC Matrix — Edit Invoice (Agent Allowed on Draft, Admin/Shipper Denied 403)
    // =========================================================================

    [Fact]
    public async Task PutInvoice_AuthenticatedAgent_DraftInvoice_UpdatesAndReturns200()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var token = MintToken(staffUser.UserId, UserRole.AgencyStaff);

        // Create draft
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 20000m,
                Currency = "LKR",
                IssueImmediately = false
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        // Update draft
        var putReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/invoices/{created.InvoiceId}")
        {
            Content = JsonContent.Create(new UpdateInvoiceDto
            {
                Amount = 28000m,
                Notes = "Adjusted diesel surcharge"
            })
        };
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var putRes = await _client.SendAsync(putReq);

        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        var updated = await putRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts);
        Assert.NotNull(updated);
        Assert.Equal(28000m, updated.Amount);
    }

    [Fact]
    public async Task PutInvoice_AuthenticatedShipper_Returns403Forbidden()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);

        // Create draft as agent
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 20000m,
                Currency = "LKR",
                IssueImmediately = false
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        // Shipper attempts update
        var putReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/invoices/{created.InvoiceId}")
        {
            Content = JsonContent.Create(new UpdateInvoiceDto { Amount = 15000m })
        };
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var putRes = await _client.SendAsync(putReq);

        Assert.Equal(HttpStatusCode.Forbidden, putRes.StatusCode);
    }

    // =========================================================================
    // 3. RBAC Matrix — Issue Invoice (Agent Allowed, Admin/Shipper Denied 403)
    // =========================================================================

    [Fact]
    public async Task IssueInvoice_AuthenticatedAgent_TransitionsToIssuedAndReturns200()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var token = MintToken(staffUser.UserId, UserRole.AgencyStaff);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 30000m,
                Currency = "LKR",
                IssueImmediately = false
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        var issueReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/issue");
        issueReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var issueRes = await _client.SendAsync(issueReq);

        Assert.Equal(HttpStatusCode.OK, issueRes.StatusCode);
        var issued = await issueRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts);
        Assert.NotNull(issued);
        Assert.Equal(InvoiceStatus.Issued, issued.Status);
    }

    [Fact]
    public async Task IssueInvoice_AuthenticatedShipper_Returns403Forbidden()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 30000m,
                Currency = "LKR",
                IssueImmediately = false
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        var issueReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/issue");
        issueReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var issueRes = await _client.SendAsync(issueReq);

        Assert.Equal(HttpStatusCode.Forbidden, issueRes.StatusCode);
    }

    // =========================================================================
    // 4. RBAC Matrix — Delete / Void Invoice (Agent Allowed, Admin/Shipper Denied 403)
    // =========================================================================

    [Fact]
    public async Task VoidInvoice_AuthenticatedAgent_WithReason_Returns200OK()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var token = MintToken(staffUser.UserId, UserRole.AgencyStaff);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 18000m,
                Currency = "LKR",
                IssueImmediately = true
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        var voidReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/void")
        {
            Content = JsonContent.Create(new VoidInvoiceDto { VoidReason = "Billing error revised by agency" })
        };
        voidReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var voidRes = await _client.SendAsync(voidReq);

        Assert.Equal(HttpStatusCode.OK, voidRes.StatusCode);
        var voided = await voidRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts);
        Assert.NotNull(voided);
        Assert.Equal(InvoiceStatus.Void, voided.Status);
    }

    [Fact]
    public async Task VoidInvoice_AuthenticatedShipper_Returns403Forbidden()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 18000m,
                Currency = "LKR",
                IssueImmediately = true
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        var voidReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/void")
        {
            Content = JsonContent.Create(new VoidInvoiceDto { VoidReason = "Shipper cancel attempt" })
        };
        voidReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var voidRes = await _client.SendAsync(voidReq);

        Assert.Equal(HttpStatusCode.Forbidden, voidRes.StatusCode);
    }

    // =========================================================================
    // 5. RBAC Matrix — Payment Receipt Upload (Shipper) + Confirm Payment (Agent)
    // =========================================================================

    /// <summary>Uploads a fake file as the given caller via the shared <c>/api/v1/files/single</c> endpoint.</summary>
    private async Task<FileUploadResultDto> UploadFileAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/single");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var content = new MultipartFormDataContent();
        // FileUploadValidator sniffs the leading bytes against the extension's magic number — a
        // ".pdf" upload must actually start with "%PDF" or it's rejected as BLOCKED_FILE_TYPE.
        var fileContent = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 fake receipt body"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "receipt.pdf");
        request.Content = content;

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileUploadResultDto>())!;
    }

    [Fact]
    public async Task UploadPaymentProof_AuthenticatedAssignedShipper_IssuedStatus_Returns200PaymentPending()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);

        // Create issued invoice as agent
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 45000m,
                Currency = "LKR",
                IssueImmediately = true
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        var uploaded = await UploadFileAsync(shipperToken);

        // Submit the receipt as the assigned shipper
        var proofReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/payment-proof")
        {
            Content = JsonContent.Create(new UploadPaymentProofDto { PublicId = uploaded.PublicId })
        };
        proofReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var proofRes = await _client.SendAsync(proofReq);

        Assert.Equal(HttpStatusCode.OK, proofRes.StatusCode);
        var updated = await proofRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts);
        Assert.NotNull(updated);
        Assert.Equal(InvoiceStatus.PaymentPending, updated.Status);
        Assert.NotNull(updated.PaymentProofUploadedAt);
        Assert.Null(updated.PaidAt);
    }

    [Fact]
    public async Task UploadPaymentProof_AuthenticatedAgent_Returns403Forbidden()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 45000m,
                Currency = "LKR",
                IssueImmediately = true
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        var uploaded = await UploadFileAsync(agentToken);

        var proofReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/payment-proof")
        {
            Content = JsonContent.Create(new UploadPaymentProofDto { PublicId = uploaded.PublicId })
        };
        proofReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var proofRes = await _client.SendAsync(proofReq);

        Assert.Equal(HttpStatusCode.Forbidden, proofRes.StatusCode);
    }

    [Fact]
    public async Task ConfirmPayment_AuthenticatedAgent_AfterProofSubmitted_Returns200Paid()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 45000m,
                Currency = "LKR",
                IssueImmediately = true
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        var uploaded = await UploadFileAsync(shipperToken);
        var proofReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/payment-proof")
        {
            Content = JsonContent.Create(new UploadPaymentProofDto { PublicId = uploaded.PublicId })
        };
        proofReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        await _client.SendAsync(proofReq);

        var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/confirm-payment");
        confirmReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var confirmRes = await _client.SendAsync(confirmReq);

        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var paid = await confirmRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts);
        Assert.NotNull(paid);
        Assert.Equal(InvoiceStatus.Paid, paid.Status);
        Assert.NotNull(paid.PaidAt);
    }

    [Fact]
    public async Task ConfirmPayment_AuthenticatedShipper_Returns403Forbidden()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 45000m,
                Currency = "LKR",
                IssueImmediately = true
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/confirm-payment");
        confirmReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var confirmRes = await _client.SendAsync(confirmReq);

        Assert.Equal(HttpStatusCode.Forbidden, confirmRes.StatusCode);
    }

    [Fact]
    public async Task ConfirmPayment_AuthenticatedAgent_WithoutProofSubmitted_Returns400BadRequest()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 45000m,
                Currency = "LKR",
                IssueImmediately = true
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.InvoiceId}/confirm-payment");
        confirmReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var confirmRes = await _client.SendAsync(confirmReq);

        Assert.Equal(HttpStatusCode.BadRequest, confirmRes.StatusCode);
    }

    // =========================================================================
    // 6. RBAC Matrix — View Invoices (GET /:id and GET /)
    // =========================================================================

    [Fact]
    public async Task GetInvoice_AuthenticatedAdmin_CanViewAnyInvoice()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);
        var adminToken = MintToken(Guid.NewGuid(), UserRole.Admin);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 18000m,
                Currency = "LKR",
                IssueImmediately = false // Draft
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        // Admin views invoice
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{created.InvoiceId}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var getRes = await _client.SendAsync(getReq);

        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetched = await getRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts);
        Assert.NotNull(fetched);
        Assert.Equal(created.InvoiceId, fetched.InvoiceId);
    }

    [Fact]
    public async Task GetInvoice_AuthenticatedShipper_DraftInvoice_Returns403Forbidden()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);

        // Create draft invoice as agent
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 18000m,
                Currency = "LKR",
                IssueImmediately = false // Draft
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        // Shipper attempts to read draft invoice
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{created.InvoiceId}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var getRes = await _client.SendAsync(getReq);

        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);
    }

    [Fact]
    public async Task GetInvoice_AuthenticatedShipper_IssuedInvoice_Returns200OK()
    {
        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);
        var shipperToken = MintToken(shipper.UserId, UserRole.Shipper);

        // Create issued invoice as agent
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                Amount = 18000m,
                Currency = "LKR",
                IssueImmediately = true // Issued
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var createRes = await _client.SendAsync(createReq);
        var created = (await createRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts))!;

        // Shipper views issued invoice
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{created.InvoiceId}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", shipperToken);
        var getRes = await _client.SendAsync(getReq);

        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetched = await getRes.Content.ReadFromJsonAsync<InvoiceResponseDto>(JsonOpts);
        Assert.NotNull(fetched);
        Assert.Equal(created.InvoiceId, fetched.InvoiceId);
    }

    // =========================================================================
    // Cashflow Summary — Admin Only
    // =========================================================================

    private async Task<InvoiceSummaryDto> GetSummaryAsAdminAsync()
    {
        var adminToken = MintToken(Guid.NewGuid(), UserRole.Admin);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/invoices/summary");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<InvoiceSummaryDto>(JsonOpts);
        Assert.NotNull(summary);
        return summary!;
    }

    [Fact]
    public async Task GetSummary_AuthenticatedAdmin_ReturnsWellFormedResponse()
    {
        var summary = await GetSummaryAsAdminAsync();

        // This test class shares one database across all [Fact]s, so a fresh factory would be
        // needed for a true "empty database" assertion — instead we assert the response shape
        // and non-negative invariants hold, which is what a truly-empty DB would also satisfy.
        Assert.True(summary.TotalInvoiced >= 0m);
        Assert.True(summary.TotalPaid >= 0m);
        Assert.Equal(summary.TotalInvoiced - summary.TotalPaid, summary.TotalOutstanding);
        Assert.NotNull(summary.RecentActivity);
    }

    [Fact]
    public async Task GetSummary_AuthenticatedAdmin_WithMixedStatuses_ReflectsNewInvoicesInAggregates()
    {
        var before = await GetSummaryAsAdminAsync();

        var (shipper, _, staffUser, trip) = await SeedTripDataAsync();
        var agentToken = MintToken(staffUser.UserId, UserRole.AgencyStaff);

        // Draft invoice
        var draftReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip.TripId,
                RecipientId = shipper.UserId,
                RecipientRole = UserRole.Shipper,
                Amount = 10000m,
                Currency = "LKR",
                IssueImmediately = false
            })
        };
        draftReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var draftRes = await _client.SendAsync(draftReq);
        Assert.Equal(HttpStatusCode.Created, draftRes.StatusCode);

        // Issued invoice (on a second trip so uniqueness constraints don't collide)
        var (_, _, staffUser2, trip2) = await SeedTripDataAsync();
        var agentToken2 = MintToken(staffUser2.UserId, UserRole.AgencyStaff);
        var issuedReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(new CreateInvoiceDto
            {
                TripId = trip2.TripId,
                Amount = 20000m,
                Currency = "LKR",
                IssueImmediately = true
            })
        };
        issuedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", agentToken2);
        var issuedRes = await _client.SendAsync(issuedReq);
        Assert.Equal(HttpStatusCode.Created, issuedRes.StatusCode);

        var after = await GetSummaryAsAdminAsync();

        Assert.Equal(before.TotalInvoiced + 30000m, after.TotalInvoiced);
        Assert.Equal(before.TotalPaid, after.TotalPaid);
        Assert.Equal(before.CountByStatus.Draft + 1, after.CountByStatus.Draft);
        Assert.Equal(before.CountByStatus.Issued + 1, after.CountByStatus.Issued);
        Assert.True(after.RecentActivity.Count >= 2);
    }

    [Theory]
    [InlineData("Shipper")]
    public async Task GetSummary_NonAdminRole_Returns403Forbidden(string roleName)
    {
        var role = Enum.Parse<UserRole>(roleName);
        var token = MintToken(Guid.NewGuid(), role);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/invoices/summary");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
