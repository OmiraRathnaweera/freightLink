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
    public async Task UpdateDriverStatus_Returns403_ForAdmin()
    {
        var (_, agencyId, driverId) = await SeedAgencyWithDriverAsync("admin-blocked-driver", "DRV-ADM");

        using var request = AuthedRequest(HttpMethod.Patch, $"/api/v1/agencies/{agencyId}/drivers/{driverId}/status", MintAdminToken());
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
}
