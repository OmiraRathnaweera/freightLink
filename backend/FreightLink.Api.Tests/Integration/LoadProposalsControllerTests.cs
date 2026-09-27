using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.LoadProposals;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="Controllers.LoadProposalsController"/>: manual Agency price
/// proposals on posted loads, and Shipper accept/reject, alongside the AI matching pipeline.
/// </summary>
public class LoadProposalsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public LoadProposalsControllerTests(CustomWebApplicationFactory factory)
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

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<(Guid ShipperUserId, Guid AgencyId, Guid StaffUserId, Guid LoadId)> SeedShipperAgencyAndLoadAsync(
        AppDbContext db, LoadStatus loadStatus = LoadStatus.Posted, AgencyStatus agencyStatus = AgencyStatus.Active)
    {
        var now = DateTimeOffset.UtcNow;

        var agencyId = Guid.NewGuid();
        db.Agencies.Add(new Agency
        {
            AgencyId = agencyId,
            Name = "Test Proposal Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "100 Port Road",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = agencyStatus,
            CreatedAt = now,
            UpdatedAt = now
        });

        var staffUserId = Guid.NewGuid();
        db.Users.Add(new User
        {
            UserId = staffUserId,
            FullName = "Staff Member",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AgencyStaff.Add(new AgencyStaff { AgencyId = agencyId, UserId = staffUserId, CreatedAt = now, UpdatedAt = now });

        var shipperUserId = Guid.NewGuid();
        db.Users.Add(new User
        {
            UserId = shipperUserId,
            FullName = "Shipper Member",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        var loadId = Guid.NewGuid();
        db.Loads.Add(new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..12],
            CargoDescription = "Test Cargo For Proposal",
            WeightKg = 3000,
            VolumeM3 = 12,
            PickupAddress = "Origin Yard",
            DropoffAddress = "Dest Yard",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffLat = 7.2m,
            DropoffLng = 80.6m,
            PickupWindowStart = now.AddDays(1),
            PickupWindowEnd = now.AddDays(2),
            Status = loadStatus,
            CreatedAt = now,
            UpdatedAt = now
        });

        await db.SaveChangesAsync();
        return (shipperUserId, agencyId, staffUserId, loadId);
    }

    private AppDbContext CreateDbScope(out IServiceScope scope)
    {
        scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    private async Task<(Guid AgencyId, Guid StaffUserId)> SeedAnotherAgencyStaffAsync(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var agencyId = Guid.NewGuid();
        db.Agencies.Add(new Agency
        {
            AgencyId = agencyId,
            Name = "Competing Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "200 Port Road",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        var staffUserId = Guid.NewGuid();
        db.Users.Add(new User
        {
            UserId = staffUserId,
            FullName = "Competing Staff",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AgencyStaff.Add(new AgencyStaff { AgencyId = agencyId, UserId = staffUserId, CreatedAt = now, UpdatedAt = now });

        await db.SaveChangesAsync();
        return (agencyId, staffUserId);
    }

    [Fact]
    public async Task Create_Returns201_ForAgencyStaff_OnPostedLoad()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (_, _, staffUserId, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var token = MintToken(staffUserId, UserRole.AgencyStaff);

            using var req = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", token);
            req.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 42000m, Message = "We can do this cheaper." });

            var res = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);

            var created = await res.Content.ReadFromJsonAsync<LoadProposalResponseDto>();
            Assert.NotNull(created);
            Assert.Equal(42000m, created.ProposedPrice);
            Assert.Equal("Pending", created.Status);
        }
    }

    [Fact]
    public async Task Create_Returns422_WhenLoadIsNotPosted()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (_, _, staffUserId, loadId) = await SeedShipperAgencyAndLoadAsync(db, LoadStatus.Matched);
            var token = MintToken(staffUserId, UserRole.AgencyStaff);

            using var req = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", token);
            req.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 42000m });

            var res = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        }
    }

    [Fact]
    public async Task Create_Returns409_WhenAgencyAlreadyHasPendingProposal()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (_, _, staffUserId, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var token = MintToken(staffUserId, UserRole.AgencyStaff);

            using var first = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", token);
            first.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 42000m });
            (await _client.SendAsync(first)).EnsureSuccessStatusCode();

            using var second = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", token);
            second.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 41000m });
            var res = await _client.SendAsync(second);

            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }
    }

    [Fact]
    public async Task Create_Returns403_ForShipper()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (shipperUserId, _, _, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var token = MintToken(shipperUserId, UserRole.Shipper);

            using var req = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", token);
            req.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 42000m });

            var res = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }

    [Fact]
    public async Task GetList_ReturnsAllProposals_ForOwningShipper()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (shipperUserId, _, staffUserId, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var staffToken = MintToken(staffUserId, UserRole.AgencyStaff);

            using var createReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", staffToken);
            createReq.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 42000m });
            (await _client.SendAsync(createReq)).EnsureSuccessStatusCode();

            var shipperToken = MintToken(shipperUserId, UserRole.Shipper);
            using var listReq = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{loadId}/proposals", shipperToken);
            var res = await _client.SendAsync(listReq);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var list = await res.Content.ReadFromJsonAsync<List<LoadProposalResponseDto>>();
            Assert.NotNull(list);
            Assert.Single(list);
        }
    }

    [Fact]
    public async Task GetList_Returns403_ForNonOwningShipper()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (_, _, _, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var otherShipperToken = MintToken(Guid.NewGuid(), UserRole.Shipper);

            using var req = AuthedRequest(HttpMethod.Get, $"/api/v1/loads/{loadId}/proposals", otherShipperToken);
            var res = await _client.SendAsync(req);

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }

    [Fact]
    public async Task Accept_Returns200_CreatesAssignment_AdvancesLoad_AndRejectsOtherProposals()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (shipperUserId, agencyId, staffUserId, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var staffToken = MintToken(staffUserId, UserRole.AgencyStaff);
            var shipperToken = MintToken(shipperUserId, UserRole.Shipper);

            // Winning proposal
            using var winReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", staffToken);
            winReq.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 40000m });
            var winRes = await _client.SendAsync(winReq);
            var winning = await winRes.Content.ReadFromJsonAsync<LoadProposalResponseDto>();

            // Competing proposal from a second agency, on the same load
            var (_, secondStaffUserId) = await SeedAnotherAgencyStaffAsync(db);
            var secondStaffToken = MintToken(secondStaffUserId, UserRole.AgencyStaff);
            using var loseReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", secondStaffToken);
            loseReq.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 45000m });
            var loseRes = await _client.SendAsync(loseReq);
            var losing = await loseRes.Content.ReadFromJsonAsync<LoadProposalResponseDto>();

            using var acceptReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals/{winning!.LoadProposalId}/accept", shipperToken);
            var acceptRes = await _client.SendAsync(acceptReq);

            Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);
            var accepted = await acceptRes.Content.ReadFromJsonAsync<LoadProposalResponseDto>();
            Assert.Equal("Accepted", accepted!.Status);

            // A fresh scope (not the seeding one) avoids reading back the seeding context's stale
            // tracked entities instead of what the request actually persisted.
            using var freshScope = _factory.Services.CreateScope();
            var freshDb = freshScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var load = await freshDb.Loads.FirstAsync(l => l.LoadId == loadId);
            Assert.Equal(LoadStatus.Matched, load.Status);

            var assignment = await freshDb.Assignments.FirstOrDefaultAsync(a => a.LoadId == loadId && a.AgencyId == agencyId);
            Assert.NotNull(assignment);
            Assert.Equal(AssignmentStatus.Accepted, assignment!.Status);
            Assert.Equal(40000m, assignment.ProposedPrice);

            var losingProposal = await freshDb.LoadProposals.FirstAsync(p => p.LoadProposalId == losing!.LoadProposalId);
            Assert.Equal(LoadProposalStatus.Rejected, losingProposal.Status);
        }
    }

    [Fact]
    public async Task Accept_Returns403_ForNonOwningShipper()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (_, _, staffUserId, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var staffToken = MintToken(staffUserId, UserRole.AgencyStaff);

            using var createReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", staffToken);
            createReq.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 40000m });
            var createRes = await _client.SendAsync(createReq);
            var created = await createRes.Content.ReadFromJsonAsync<LoadProposalResponseDto>();

            var otherShipperToken = MintToken(Guid.NewGuid(), UserRole.Shipper);
            using var acceptReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals/{created!.LoadProposalId}/accept", otherShipperToken);
            var res = await _client.SendAsync(acceptReq);

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }

    [Fact]
    public async Task Reject_Returns200_ForOwningShipper()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (shipperUserId, _, staffUserId, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var staffToken = MintToken(staffUserId, UserRole.AgencyStaff);
            var shipperToken = MintToken(shipperUserId, UserRole.Shipper);

            using var createReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", staffToken);
            createReq.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 40000m });
            var createRes = await _client.SendAsync(createReq);
            var created = await createRes.Content.ReadFromJsonAsync<LoadProposalResponseDto>();

            using var rejectReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals/{created!.LoadProposalId}/reject", shipperToken);
            rejectReq.Content = JsonContent.Create(new RespondLoadProposalDto { Reason = "Too expensive." });
            var res = await _client.SendAsync(rejectReq);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var rejected = await res.Content.ReadFromJsonAsync<LoadProposalResponseDto>();
            Assert.Equal("Rejected", rejected!.Status);
        }
    }

    [Fact]
    public async Task Withdraw_Returns200_ForProposingAgency()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (_, _, staffUserId, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var staffToken = MintToken(staffUserId, UserRole.AgencyStaff);

            using var createReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", staffToken);
            createReq.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 40000m });
            var createRes = await _client.SendAsync(createReq);
            var created = await createRes.Content.ReadFromJsonAsync<LoadProposalResponseDto>();

            using var withdrawReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals/{created!.LoadProposalId}/withdraw", staffToken);
            var res = await _client.SendAsync(withdrawReq);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var withdrawn = await res.Content.ReadFromJsonAsync<LoadProposalResponseDto>();
            Assert.Equal("Withdrawn", withdrawn!.Status);
        }
    }

    [Fact]
    public async Task Withdraw_Returns403_ForOtherAgency()
    {
        var db = CreateDbScope(out var scope);
        using (scope)
        {
            var (_, _, staffUserId, loadId) = await SeedShipperAgencyAndLoadAsync(db);
            var staffToken = MintToken(staffUserId, UserRole.AgencyStaff);

            using var createReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals", staffToken);
            createReq.Content = JsonContent.Create(new CreateLoadProposalDto { ProposedPrice = 40000m });
            var createRes = await _client.SendAsync(createReq);
            var created = await createRes.Content.ReadFromJsonAsync<LoadProposalResponseDto>();

            var (_, otherStaffUserId) = await SeedAnotherAgencyStaffAsync(db);
            var otherStaffToken = MintToken(otherStaffUserId, UserRole.AgencyStaff);

            using var withdrawReq = AuthedRequest(HttpMethod.Post, $"/api/v1/loads/{loadId}/proposals/{created!.LoadProposalId}/withdraw", otherStaffToken);
            var res = await _client.SendAsync(withdrawReq);

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }
}
