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
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

public class AgenciesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AgenciesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<TokenResponseDto> RegisterAndLoginAgencyAsync(string emailPrefix, string regNoPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        var regNo = $"{regNoPrefix}-{Guid.NewGuid():N}";
        const string password = "Sup3r$ecret1";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register/agency", new RegisterAgencyRequestDto
        {
            Email = email,
            Password = password,
            FullName = "Integration Tester",
            AgencyName = "Test Agency",
            BusinessRegNo = regNo,
            YardAddress = "123 Main Street",
            YardLat = 1.0m,
            YardLng = 1.0m
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    private static string MintAdminToken()
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-that-is-long-enough-1234567890"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task GetAllAgencies_Returns200_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/agencies", MintAdminToken());
        var response = await _client.SendAsync(request);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllAgencies_Returns403_ForAgencyStaff()
    {
        var tokens = await RegisterAndLoginAgencyAsync("list-fail", "FAIL");
        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/agencies", tokens.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Directly inserts <paramref name="count"/> minimal agencies (bypassing the real registration
    /// flow, which would be far slower for a "many agencies" pagination test), each with
    /// <paramref name="marker"/> embedded in its Name so a test can isolate exactly its own seeded
    /// rows via <c>?search=</c>, immune to any other agencies other tests in this shared-per-class
    /// InMemory DB (<c>IClassFixture</c>) may have created. CreatedAt descends by one second per
    /// agency in seed order, so seed order is deterministic default (createdAt desc) sort order.
    /// </summary>
    private async Task<List<Guid>> SeedManyAgenciesAsync(string marker, int count, AgencyStatus status = AgencyStatus.Active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var agencyId = Guid.NewGuid();
            ids.Add(agencyId);
            db.Agencies.Add(new Agency
            {
                AgencyId = agencyId,
                Name = $"{marker} Agency {i:D3}",
                BusinessRegNo = $"BR-{marker}-{i:D3}",
                YardAddress = $"{i} Test Yard Road",
                YardLat = 6.9m,
                YardLng = 79.8m,
                Status = status,
                CreatedAt = now.AddSeconds(-i),
                UpdatedAt = now.AddSeconds(-i)
            });
        }
        await db.SaveChangesAsync();
        return ids;
    }

    [Fact]
    public async Task GetAllAgencies_PagesAndSearchTogether_FindEveryMatchAcrossAllPages()
    {
        // Regression test for issue #45: the admin console previously only ever showed page 1 and
        // searched/filtered only those already-fetched rows, so a match that only existed beyond
        // page 1 looked like it didn't exist. This seeds 25 agencies sharing one unique marker and
        // asserts paging through the *search-filtered* result set (not the whole table) surfaces
        // every one of them exactly once, including the ones on page 2 and 3.
        var marker = $"Zephyr{Guid.NewGuid():N}"[..14];
        var seededIds = await SeedManyAgenciesAsync(marker, 25);

        var foundIds = new HashSet<Guid>();
        int? totalItems = null;
        int? totalPages = null;

        for (var page = 1; page <= 3; page++)
        {
            using var request = AuthedRequest(HttpMethod.Get, $"/api/v1/agencies?search={marker}&page={page}&pageSize=10", MintAdminToken());
            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<PagedAgencyResponseDto>(JsonOpts);
            Assert.NotNull(body);
            Assert.Equal(page, body!.Page);
            Assert.All(body.Items, item => Assert.Contains(marker, item.Name));

            totalItems = body.TotalItems;
            totalPages = body.TotalPages;
            foreach (var item in body.Items) foundIds.Add(item.AgencyId);
        }

        Assert.Equal(25, totalItems);
        Assert.Equal(3, totalPages);
        Assert.Equal(seededIds.Count, foundIds.Count);
        Assert.True(seededIds.All(id => foundIds.Contains(id)), "Every seeded agency should have been found across the paged, search-filtered results.");
    }

    [Fact]
    public async Task GetPlatformSummary_Returns200_WithSystemWideCounts_UnaffectedByListFilters()
    {
        // Regression test for issue #45: the summary cards must reflect true platform-wide totals,
        // not just whatever happens to be on the admin's current (filtered/paged) list view. Since
        // this InMemory DB is shared across every test in this class (IClassFixture), we assert the
        // *delta* this seed produces rather than an absolute count, so the test is immune to
        // whatever other tests have already added.
        using var beforeReq = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/summary", MintAdminToken());
        var beforeRes = await _client.SendAsync(beforeReq);
        Assert.Equal(HttpStatusCode.OK, beforeRes.StatusCode);
        var before = await beforeRes.Content.ReadFromJsonAsync<AgencyPlatformSummaryDto>(JsonOpts);
        Assert.NotNull(before);

        var marker = $"Summary{Guid.NewGuid():N}"[..14];
        await SeedManyAgenciesAsync(marker, 7, AgencyStatus.Active);
        await SeedManyAgenciesAsync(marker, 3, AgencyStatus.Suspended);

        using var afterReq = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/summary", MintAdminToken());
        var afterRes = await _client.SendAsync(afterReq);
        var after = await afterRes.Content.ReadFromJsonAsync<AgencyPlatformSummaryDto>(JsonOpts);
        Assert.NotNull(after);

        Assert.Equal(before!.TotalAgencies + 10, after!.TotalAgencies);
        Assert.Equal(before.ActiveAgencies + 7, after.ActiveAgencies);

        // A search/status-scoped list call for just this seed must NOT change what the summary
        // reports - the summary endpoint takes no filters at all and must stay platform-wide.
        using var scopedListReq = AuthedRequest(HttpMethod.Get, $"/api/v1/agencies?search={marker}&status=Active&pageSize=1", MintAdminToken());
        await _client.SendAsync(scopedListReq);

        using var afterListReq = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/summary", MintAdminToken());
        var afterListRes = await _client.SendAsync(afterListReq);
        var afterList = await afterListRes.Content.ReadFromJsonAsync<AgencyPlatformSummaryDto>(JsonOpts);
        Assert.Equal(after.TotalAgencies, afterList!.TotalAgencies);
    }

    [Fact]
    public async Task GetPlatformSummary_Returns403_ForAgencyStaff()
    {
        var tokens = await RegisterAndLoginAgencyAsync("summary-fail", "SUMFAIL");
        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/summary", tokens.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAgency_Returns403_ForAgencyStaff_OnUnownedAgency()
    {
        var agency1 = await RegisterAndLoginAgencyAsync("update-1", "U1");
        var agency2 = await RegisterAndLoginAgencyAsync("update-2", "U2");

        // Admin gets the ID of agency 2
        using var listReq = AuthedRequest(HttpMethod.Get, "/api/v1/agencies", MintAdminToken());
        var listRes = await _client.SendAsync(listReq);
        var page = await listRes.Content.ReadFromJsonAsync<PagedAgencyResponseDto>(JsonOpts);
        var targetAgencyId = page!.Items.Last().AgencyId;

        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{targetAgencyId}", agency1.AccessToken);
        request.Content = JsonContent.Create(new AgencyUpdateDto { Name = "Hacked Name" });
        
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddDriver_Returns201_ForAgencyStaff()
    {
        var agency = await RegisterAndLoginAgencyAsync("add-driver", "DRV-ADD");

        // Fetch agency id via /my/fleet
        using var fleetReq = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/my/fleet", agency.AccessToken);
        var fleetRes = await _client.SendAsync(fleetReq);
        fleetRes.EnsureSuccessStatusCode();
        var fleet = await fleetRes.Content.ReadFromJsonAsync<AgencyFleetResponseDto>(JsonOpts);
        var agencyId = fleet!.AgencyId;

        // Add driver
        var driverEmail = $"staff-driver-{Guid.NewGuid():N}@example.com";
        var licenceNo = $"DL-{Guid.NewGuid():N}".Substring(0, 15);
        using var addReq = AuthedRequest(HttpMethod.Post, $"/api/v1/agencies/{agencyId}/drivers", agency.AccessToken);
        addReq.Content = JsonContent.Create(new CreateDriverRequestDto
        {
            Email = driverEmail,
            FullName = "Employed Driver",
            PhoneE164 = "+94771234567",
            LicenceNo = licenceNo,
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2))
        });

        var addRes = await _client.SendAsync(addReq);
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);
        var created = await addRes.Content.ReadFromJsonAsync<DriverResponseDto>(JsonOpts);
        Assert.NotNull(created);
        Assert.Equal(driverEmail, created.Email);
        Assert.Equal("Employed Driver", created.FullName);
        Assert.False(string.IsNullOrWhiteSpace(created.TemporaryPassword));

        // The emailed/returned temporary password must actually work for login.
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto
        {
            Email = driverEmail,
            Password = created.TemporaryPassword!
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // GetDrivers
        using var listReq = AuthedRequest(HttpMethod.Get, $"/api/v1/agencies/{agencyId}/drivers", agency.AccessToken);
        var listRes = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var drivers = await listRes.Content.ReadFromJsonAsync<List<DriverResponseDto>>(JsonOpts);
        Assert.NotNull(drivers);
        Assert.Contains(drivers, d => d.Email == driverEmail);
        // The temporary password is shown exactly once, in the creation response — never on a re-list.
        Assert.All(drivers, d => Assert.Null(d.TemporaryPassword));
    }

    private async Task<(string AgencyStaffToken, Guid AgencyId, Guid DriverId)> SeedAgencyWithDriverAsync(string emailPrefix, string regNoPrefix)
    {
        var agency = await RegisterAndLoginAgencyAsync(emailPrefix, regNoPrefix);

        using var fleetReq = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/my/fleet", agency.AccessToken);
        var fleetRes = await _client.SendAsync(fleetReq);
        fleetRes.EnsureSuccessStatusCode();
        var fleet = await fleetRes.Content.ReadFromJsonAsync<AgencyFleetResponseDto>(JsonOpts);
        var agencyId = fleet!.AgencyId;

        using var addReq = AuthedRequest(HttpMethod.Post, $"/api/v1/agencies/{agencyId}/drivers", agency.AccessToken);
        addReq.Content = JsonContent.Create(new CreateDriverRequestDto
        {
            Email = $"{emailPrefix}-driver-{Guid.NewGuid():N}@example.com",
            FullName = "Roster Driver",
            LicenceNo = $"DL-{Guid.NewGuid():N}".Substring(0, 15),
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2))
        });
        var addRes = await _client.SendAsync(addReq);
        addRes.EnsureSuccessStatusCode();
        var created = await addRes.Content.ReadFromJsonAsync<DriverResponseDto>(JsonOpts);

        return (agency.AccessToken, agencyId, created!.DriverId);
    }

    private async Task<(string AgencyStaffToken, Guid AgencyId, Guid VehicleId)> SeedActiveAgencyWithVehicleAsync(string prefix)
    {
        var tokens = await RegisterAndLoginAgencyAsync(prefix, prefix.ToUpperInvariant());
        using var fleetRequest = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/my/fleet", tokens.AccessToken);
        var fleetResponse = await _client.SendAsync(fleetRequest);
        fleetResponse.EnsureSuccessStatusCode();
        var fleet = await fleetResponse.Content.ReadFromJsonAsync<AgencyFleetResponseDto>(JsonOpts);
        var agencyId = fleet!.AgencyId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var agency = await db.Agencies.SingleAsync(a => a.AgencyId == agencyId);
            agency.Status = AgencyStatus.Active;
            await db.SaveChangesAsync();
        }

        using var addRequest = AuthedRequest(HttpMethod.Post, $"/api/v1/agencies/{agencyId}/vehicles", tokens.AccessToken);
        addRequest.Content = JsonContent.Create(new VehicleCreateDto
        {
            RegistrationNo = $"WP-CAB-{Guid.NewGuid():N}"[..15],
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000m,
            VolumeM3 = 18m
        });
        var addResponse = await _client.SendAsync(addRequest);
        addResponse.EnsureSuccessStatusCode();
        var created = await addResponse.Content.ReadFromJsonAsync<VehicleResponseDto>(JsonOpts);
        return (tokens.AccessToken, agencyId, created!.VehicleId);
    }

    [Fact]
    public async Task UpdateVehicle_UpdatesOwnedVehicleDetails_WithoutChangingStatus()
    {
        var (token, agencyId, vehicleId) = await SeedActiveAgencyWithVehicleAsync("edit-vehicle");
        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}", token);
        request.Content = JsonContent.Create(new VehicleUpdateDto
        {
            RegistrationNo = "WP-CAB-2468",
            VehicleType = VehicleType.Container,
            CapacityKg = 24000m,
            VolumeM3 = 60m
        });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<VehicleResponseDto>(JsonOpts);
        Assert.NotNull(updated);
        Assert.Equal("WP-CAB-2468", updated.RegistrationNo);
        Assert.Equal("Container", updated.VehicleType);
        Assert.Equal(24000m, updated.CapacityKg);
        Assert.Equal(60m, updated.VolumeM3);
        Assert.Equal("Available", updated.Status);

        using var listRequest = AuthedRequest(HttpMethod.Get, $"/api/v1/agencies/{agencyId}/vehicles", token);
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var vehicles = await listResponse.Content.ReadFromJsonAsync<List<VehicleResponseDto>>(JsonOpts);
        Assert.Contains(vehicles!, v => v.VehicleId == vehicleId && v.RegistrationNo == "WP-CAB-2468");
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsConflict_ForDuplicateAgencyRegistration()
    {
        var (token, agencyId, vehicleId) = await SeedActiveAgencyWithVehicleAsync("duplicate-vehicle");
        using var addRequest = AuthedRequest(HttpMethod.Post, $"/api/v1/agencies/{agencyId}/vehicles", token);
        addRequest.Content = JsonContent.Create(new VehicleCreateDto
        {
            RegistrationNo = "WP-CAB-3579",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 4000m,
            VolumeM3 = 16m
        });
        (await _client.SendAsync(addRequest)).EnsureSuccessStatusCode();

        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}", token);
        request.Content = JsonContent.Create(new VehicleUpdateDto
        {
            RegistrationNo = "WP-CAB-3579",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000m,
            VolumeM3 = 18m
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsForbidden_ForAnotherAgencyAndAdmin()
    {
        var (_, agencyId, vehicleId) = await SeedActiveAgencyWithVehicleAsync("owned-vehicle");
        var otherAgency = await RegisterAndLoginAgencyAsync("other-vehicle", "OTHER-VEHICLE");
        var body = new VehicleUpdateDto
        {
            RegistrationNo = "WP-CAB-4680",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000m,
            VolumeM3 = 18m
        };

        using var otherRequest = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}", otherAgency.AccessToken);
        otherRequest.Content = JsonContent.Create(body);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(otherRequest)).StatusCode);

        using var adminRequest = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}", MintAdminToken());
        adminRequest.Content = JsonContent.Create(body);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(adminRequest)).StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsValidationError_ForZeroCapacity()
    {
        var (token, agencyId, vehicleId) = await SeedActiveAgencyWithVehicleAsync("invalid-vehicle");
        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}", token);
        request.Content = JsonContent.Create(new VehicleUpdateDto
        {
            RegistrationNo = "WP-CAB-5791",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 0,
            VolumeM3 = 18m
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task VehicleMutations_ReturnValidationErrors_WhenTypeOrStatusIsMissing()
    {
        var (token, agencyId, vehicleId) = await SeedActiveAgencyWithVehicleAsync("missing-vehicle-fields");

        using var editRequest = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}", token);
        editRequest.Content = JsonContent.Create(new VehicleUpdateDto
        {
            RegistrationNo = "WP-CAB-5791",
            CapacityKg = 5000m,
            VolumeM3 = 18m
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(editRequest)).StatusCode);

        using var statusRequest = AuthedRequest(HttpMethod.Patch, $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}/status", token);
        statusRequest.Content = JsonContent.Create(new UpdateVehicleStatusDto());
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(statusRequest)).StatusCode);
    }

    [Fact]
    public async Task VehicleStatus_TransitionsAvailableMaintenanceAndRetired_ButCannotRestoreRetired()
    {
        var (token, agencyId, vehicleId) = await SeedActiveAgencyWithVehicleAsync("status-vehicle");
        var path = $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}/status";

        using var maintenanceRequest = AuthedRequest(HttpMethod.Patch, path, token);
        maintenanceRequest.Content = JsonContent.Create(new UpdateVehicleStatusDto { Status = VehicleStatus.Maintenance });
        var maintenance = await _client.SendAsync(maintenanceRequest);
        Assert.Equal(HttpStatusCode.OK, maintenance.StatusCode);

        using var availableRequest = AuthedRequest(HttpMethod.Patch, path, token);
        availableRequest.Content = JsonContent.Create(new UpdateVehicleStatusDto { Status = VehicleStatus.Available });
        var available = await _client.SendAsync(availableRequest);
        Assert.Equal(HttpStatusCode.OK, available.StatusCode);

        using var retireRequest = AuthedRequest(HttpMethod.Patch, path, token);
        retireRequest.Content = JsonContent.Create(new UpdateVehicleStatusDto { Status = VehicleStatus.Retired });
        var retired = await _client.SendAsync(retireRequest);
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);

        using var restoreRequest = AuthedRequest(HttpMethod.Patch, path, token);
        restoreRequest.Content = JsonContent.Create(new UpdateVehicleStatusDto { Status = VehicleStatus.Available });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.SendAsync(restoreRequest)).StatusCode);

        using var editRequest = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}", token);
        editRequest.Content = JsonContent.Create(new VehicleUpdateDto
        {
            RegistrationNo = "WP-CAB-6802",
            VehicleType = VehicleType.Container,
            CapacityKg = 24000m,
            VolumeM3 = 60m
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.SendAsync(editRequest)).StatusCode);
    }

    [Fact]
    public async Task VehicleStatus_RejectsDirectOnTrip_AndOtherAgency()
    {
        var (token, agencyId, vehicleId) = await SeedActiveAgencyWithVehicleAsync("ontrip-vehicle");
        var path = $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}/status";
        using var onTripRequest = AuthedRequest(HttpMethod.Patch, path, token);
        onTripRequest.Content = JsonContent.Create(new UpdateVehicleStatusDto { Status = VehicleStatus.OnTrip });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.SendAsync(onTripRequest)).StatusCode);

        var otherAgency = await RegisterAndLoginAgencyAsync("unowned-vehicle", "UNOWNED-VEHICLE");
        using var otherRequest = AuthedRequest(HttpMethod.Patch, path, otherAgency.AccessToken);
        otherRequest.Content = JsonContent.Create(new UpdateVehicleStatusDto { Status = VehicleStatus.Maintenance });
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(otherRequest)).StatusCode);

        using var adminRequest = AuthedRequest(HttpMethod.Patch, path, MintAdminToken());
        adminRequest.Content = JsonContent.Create(new UpdateVehicleStatusDto { Status = VehicleStatus.Maintenance });
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(adminRequest)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vehicle = await db.Vehicles.SingleAsync(v => v.VehicleId == vehicleId);
            vehicle.Status = VehicleStatus.OnTrip;
            await db.SaveChangesAsync();
        }
        using var editRequest = AuthedRequest(HttpMethod.Put, $"/api/v1/agencies/{agencyId}/vehicles/{vehicleId}", token);
        editRequest.Content = JsonContent.Create(new VehicleUpdateDto
        {
            RegistrationNo = "WP-CAB-7913",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000m,
            VolumeM3 = 18m
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.SendAsync(editRequest)).StatusCode);
    }

    [Fact]
    public async Task UpdateDriverStatus_Returns200_AndDeactivatesDriver_ForOwningAgencyStaff()
    {
        var (agencyStaffToken, agencyId, driverId) = await SeedAgencyWithDriverAsync("deactivate-driver", "DRV-DEACT");

        using var request = AuthedRequest(HttpMethod.Patch, $"/api/v1/agencies/{agencyId}/drivers/{driverId}/status", agencyStaffToken);
        request.Content = JsonContent.Create(new UpdateDriverStatusDto { Status = DriverStatus.Inactive });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<DriverResponseDto>(JsonOpts);
        Assert.Equal(DriverStatus.Inactive, updated!.Status);
    }

    [Fact]
    public async Task UpdateDriverStatus_Returns200_AndReactivatesDriver()
    {
        var (agencyStaffToken, agencyId, driverId) = await SeedAgencyWithDriverAsync("reactivate-driver", "DRV-REACT");

        using var deactivateReq = AuthedRequest(HttpMethod.Patch, $"/api/v1/agencies/{agencyId}/drivers/{driverId}/status", agencyStaffToken);
        deactivateReq.Content = JsonContent.Create(new UpdateDriverStatusDto { Status = DriverStatus.Inactive });
        (await _client.SendAsync(deactivateReq)).EnsureSuccessStatusCode();

        using var reactivateReq = AuthedRequest(HttpMethod.Patch, $"/api/v1/agencies/{agencyId}/drivers/{driverId}/status", agencyStaffToken);
        reactivateReq.Content = JsonContent.Create(new UpdateDriverStatusDto { Status = DriverStatus.Active });
        var response = await _client.SendAsync(reactivateReq);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<DriverResponseDto>(JsonOpts);
        Assert.Equal(DriverStatus.Active, updated!.Status);
    }

    [Fact]
    public async Task UpdateDriverStatus_Returns422_WhenSettingOnTripDirectly()
    {
        var (agencyStaffToken, agencyId, driverId) = await SeedAgencyWithDriverAsync("ontrip-driver", "DRV-ONTRIP");

        using var request = AuthedRequest(HttpMethod.Patch, $"/api/v1/agencies/{agencyId}/drivers/{driverId}/status", agencyStaffToken);
        request.Content = JsonContent.Create(new UpdateDriverStatusDto { Status = DriverStatus.OnTrip });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDriverStatus_Returns403_ForUnownedAgency()
    {
        var (_, agencyId, driverId) = await SeedAgencyWithDriverAsync("owned-driver", "DRV-OWN");
        var otherAgency = await RegisterAndLoginAgencyAsync("other-agency", "DRV-OTHER");

        using var request = AuthedRequest(HttpMethod.Patch, $"/api/v1/agencies/{agencyId}/drivers/{driverId}/status", otherAgency.AccessToken);
        request.Content = JsonContent.Create(new UpdateDriverStatusDto { Status = DriverStatus.Inactive });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetExpiringCompliance_Returns200_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/expiring-compliance?days=30", MintAdminToken());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetExpiringCompliance_Returns403_ForAgencyStaff()
    {
        var tokens = await RegisterAndLoginAgencyAsync("expiring-fail", "EXPFAIL");
        using var request = AuthedRequest(HttpMethod.Get, "/api/v1/agencies/expiring-compliance", tokens.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpRequestMessage StatusRequest(Guid agencyId, string token, AgencyStatus status, string reason)
    {
        var request = AuthedRequest(HttpMethod.Patch, $"/api/v1/agencies/{agencyId}/status", token);
        request.Content = JsonContent.Create(new UpdateAgencyStatusDto { Status = status, Reason = reason });
        return request;
    }

    [Fact]
    public async Task UpdateAgencyStatus_Returns200_AndReactivatesSuspendedAgency_ForAdmin()
    {
        var agencyId = (await SeedManyAgenciesAsync($"Reac{Guid.NewGuid():N}"[..12], 1, AgencyStatus.Suspended))[0];

        using var request = StatusRequest(agencyId, MintAdminToken(), AgencyStatus.Active, "Suspended in error");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AgencyResponseDto>(JsonOpts);
        Assert.Equal(AgencyStatus.Active, body!.Status);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AgencyStatusHistories.SingleAsync(h => h.AgencyId == agencyId);
        Assert.Equal(AgencyStatus.Suspended, audit.FromStatus);
        Assert.Equal(AgencyStatus.Active, audit.ToStatus);
        Assert.Equal("Suspended in error", audit.Reason);
    }

    [Fact]
    public async Task UpdateAgencyStatus_Returns400_ForInvalidTransition()
    {
        var agencyId = (await SeedManyAgenciesAsync($"Inv{Guid.NewGuid():N}"[..12], 1, AgencyStatus.Pending))[0];

        using var request = StatusRequest(agencyId, MintAdminToken(), AgencyStatus.Active, "Skip verification");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("INVALID_AGENCY_STATUS_TRANSITION", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateAgencyStatus_Returns400_WhenReasonMissing()
    {
        var agencyId = (await SeedManyAgenciesAsync($"NoRsn{Guid.NewGuid():N}"[..12], 1, AgencyStatus.Suspended))[0];

        using var request = StatusRequest(agencyId, MintAdminToken(), AgencyStatus.Active, "");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAgencyStatus_Returns403_ForAgencyStaff()
    {
        var tokens = await RegisterAndLoginAgencyAsync("status-fail", "STFAIL");
        var agencyId = (await SeedManyAgenciesAsync($"Forb{Guid.NewGuid():N}"[..12], 1, AgencyStatus.Suspended))[0];

        using var request = StatusRequest(agencyId, tokens.AccessToken, AgencyStatus.Active, "Self-reactivation attempt");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
