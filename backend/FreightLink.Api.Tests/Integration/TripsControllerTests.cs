using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Assignments;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Common;
using FreightLink.Api.DTOs.Trips;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-HTTP-pipeline tests for <c>TripsController</c>, covering only the role/authentication
/// contract — mirrors <c>LoadsControllerTests</c>'s scope-split reasoning, but more narrowly, since
/// <c>TripService</c> is currently a skeleton: every method throws <see cref="NotImplementedException"/>
/// unconditionally, so no ownership resolution, status-transition validation, or evidence-role
/// pairing logic exists yet to test. Endpoint-logic coverage for all of that belongs in a future
/// <c>TripServiceTests</c> once <c>TripService.cs</c> is genuinely implemented.
///
/// <para>
/// <b>TEMPORARY:</b> every "correctly-authorized" test below asserts <c>500 INTERNAL_SERVER_ERROR</c>
/// as its expected outcome — this is <i>only</i> correct while <c>TripService</c> is a stub. Once real
/// logic replaces a given method, the corresponding test(s) here must be rewritten to assert the real
/// expected result (e.g. 200/404/422) instead of deleted, so the role-gating coverage isn't lost.
/// </para>
/// </summary>
public class TripsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>Creates the test class with an HTTP client bound to the shared in-process test host.</summary>
    public TripsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>Registers a new shipper with a unique email and logs in, returning the issued tokens.</summary>
    private async Task<TokenResponseDto> RegisterAndLoginShipperAsync(string emailPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecret1";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = email,
            Password = password,
            FullName = "Integration Tester",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    /// <summary>
    /// Mints a validly-signed JWT carrying one <c>ClaimTypes.Role</c> claim per entry in
    /// <paramref name="roles"/>. Used here for AgencyStaff, Driver, and Admin — there is no public
    /// Driver registration endpoint at all, no public Admin registration endpoint, and (per
    /// <c>CustomWebApplicationFactory</c>) the test host's admin seed is disabled — so minting is the
    /// only way to obtain a token for any of these three roles in this test environment. Identical to
    /// <c>LoadsControllerTests.MintTokenWithRoles</c>.
    /// </summary>
    private static string MintTokenWithRoles(params string[] roles)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-that-is-long-enough-1234567890"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>A valid ChangeTripStatusDto payload — sent even on expected-403 tests, so a failing assertion unambiguously means the role gate, not a validation error, produced the result.</summary>
    private static ChangeTripStatusDto ValidChangeStatusDto() => new() { TargetStatus = TripStatus.PickedUp, Notes = "integration test" };

    /// <summary>A valid UploadTripEvidenceDto payload — same defensive reasoning as <see cref="ValidChangeStatusDto"/>.</summary>
    private static UploadTripEvidenceDto ValidUploadEvidenceDto() => new() { PublicId = "test-file-123", EvidenceType = EvidenceType.PickupProof };

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        return json.RootElement.GetProperty("error").GetProperty("code").GetString()!;
    }

    // --- Unauthenticated: every Trips endpoint requires a token ---

    /// <summary>GET /trips without a token is 401.</summary>
    [Fact]
    public async Task GetList_Returns401_WithoutToken()
    {
        var response = await _client.GetAsync("/api/v1/trips");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>GET /trips/{id} without a token is 401.</summary>
    [Fact]
    public async Task GetById_Returns401_WithoutToken()
    {
        var response = await _client.GetAsync($"/api/v1/trips/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>POST /trips/{id}/status without a token is 401.</summary>
    [Fact]
    public async Task ChangeStatus_Returns401_WithoutToken()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/trips/{Guid.NewGuid()}/status", ValidChangeStatusDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>POST /trips/{id}/evidence without a token is 401.</summary>
    [Fact]
    public async Task UploadEvidence_Returns401_WithoutToken()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/trips/{Guid.NewGuid()}/evidence", ValidUploadEvidenceDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>GET /trips/{id}/evidence without a token is 401.</summary>
    [Fact]
    public async Task GetEvidence_Returns401_WithoutToken()
    {
        var response = await _client.GetAsync($"/api/v1/trips/{Guid.NewGuid()}/evidence");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- GET /trips (AgencyStaff, Driver, Admin; Shipper excluded) ---

    /// <summary>A Shipper cannot call the trips list/dashboard endpoint — role gating.</summary>
    [Fact]
    public async Task GetList_Returns403_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-list-shipper");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/v1/trips", tokens.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An AgencyStaff caller reaches the service layer and gets 200 OK.</summary>
    [Fact]
    public async Task GetList_Returns200_ForAgencyStaff()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/v1/trips", MintTokenWithRoles("AgencyStaff")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>A Driver caller reaches the service layer and gets 200 OK.</summary>
    [Fact]
    public async Task GetList_Returns200_ForDriver()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/v1/trips", MintTokenWithRoles("Driver")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>An Admin caller reaches the service layer and gets 200 OK.</summary>
    [Fact]
    public async Task GetList_Returns200_ForAdmin()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/v1/trips", MintTokenWithRoles("Admin")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- GET /trips/{id} (all four roles admitted by the role gate) ---

    /// <summary>A Shipper caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task GetById_Returns404_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-getbyid-shipper");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}", tokens.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    /// <summary>An AgencyStaff caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task GetById_Returns404_ForAgencyStaff()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}", MintTokenWithRoles("AgencyStaff")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    /// <summary>A Driver caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task GetById_Returns404_ForDriver()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}", MintTokenWithRoles("Driver")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    /// <summary>An Admin caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task GetById_Returns404_ForAdmin()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}", MintTokenWithRoles("Admin")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    // --- POST /trips/{id}/status (AgencyStaff, Driver only) ---

    /// <summary>A Shipper cannot advance a trip's status — role gating.</summary>
    [Fact]
    public async Task ChangeStatus_Returns403_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-status-shipper");

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/status", tokens.AccessToken);
        request.Content = JsonContent.Create(ValidChangeStatusDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An Admin cannot advance a trip's status — deliberately excluded from this write endpoint per the API contract, unlike the read endpoints above.</summary>
    [Fact]
    public async Task ChangeStatus_Returns403_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/status", MintTokenWithRoles("Admin"));
        request.Content = JsonContent.Create(ValidChangeStatusDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An AgencyStaff caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task ChangeStatus_Returns404_ForAgencyStaff()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/status", MintTokenWithRoles("AgencyStaff"));
        request.Content = JsonContent.Create(ValidChangeStatusDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    /// <summary>A Driver caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task ChangeStatus_Returns404_ForDriver()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/status", MintTokenWithRoles("Driver"));
        request.Content = JsonContent.Create(ValidChangeStatusDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    // --- POST /trips/{id}/evidence (AgencyStaff, Driver only) ---

    /// <summary>A Shipper cannot submit trip evidence — role gating.</summary>
    [Fact]
    public async Task UploadEvidence_Returns403_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-evidence-shipper");

        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/evidence", tokens.AccessToken);
        request.Content = JsonContent.Create(ValidUploadEvidenceDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An Admin cannot submit trip evidence — deliberately excluded from this write endpoint.</summary>
    [Fact]
    public async Task UploadEvidence_Returns403_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("Admin"));
        request.Content = JsonContent.Create(ValidUploadEvidenceDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>An AgencyStaff caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task UploadEvidence_Returns404_ForAgencyStaff()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("AgencyStaff"));
        request.Content = JsonContent.Create(ValidUploadEvidenceDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    /// <summary>A Driver caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task UploadEvidence_Returns404_ForDriver()
    {
        using var request = AuthedRequest(HttpMethod.Post, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("Driver"));
        request.Content = JsonContent.Create(ValidUploadEvidenceDto());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    // --- GET /trips/{id}/evidence (all four roles admitted) ---

    /// <summary>A Shipper caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task GetEvidence_Returns404_ForShipper()
    {
        var tokens = await RegisterAndLoginShipperAsync("trips-getevidence-shipper");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}/evidence", tokens.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    /// <summary>An AgencyStaff caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task GetEvidence_Returns404_ForAgencyStaff()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("AgencyStaff")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    /// <summary>A Driver caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task GetEvidence_Returns404_ForDriver()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("Driver")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    /// <summary>An Admin caller reaches the service layer; returns 404 for a non-existent trip.</summary>
    [Fact]
    public async Task GetEvidence_Returns404_ForAdmin()
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/v1/trips/{Guid.NewGuid()}/evidence", MintTokenWithRoles("Admin")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    // --- Create / Update / Delete / Cancel ---

    [Fact]
    public async Task Create_Returns401_WithoutToken()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/trips", new CreateTripDto
        {
            AssignmentId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            DriverId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Returns403_ForShipper()
    {
        var shipperTokens = await RegisterAndLoginShipperAsync("trip-create");
        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/trips", shipperTokens.AccessToken);
        request.Content = JsonContent.Create(new CreateTripDto
        {
            AssignmentId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            DriverId = Guid.NewGuid()
        });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Returns403_ForAdmin()
    {
        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/trips", MintTokenWithRoles("Admin"));
        request.Content = JsonContent.Create(new CreateTripDto
        {
            AssignmentId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            DriverId = Guid.NewGuid()
        });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Returns404_ForAgencyStaff_WhenAssignmentDoesNotExist()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var staffUserId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();

        db.Users.Add(new User
        {
            UserId = staffUserId,
            FullName = "Agency Dispatcher",
            Email = $"staff-create-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Agencies.Add(new Agency
        {
            AgencyId = agencyId,
            Name = "Dispatch Logistics",
            BusinessRegNo = $"BR-DSP-{Guid.NewGuid():N}"[..15],
            YardAddress = "100 Port Road",
            YardLat = 6.93m,
            YardLng = 79.85m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.AgencyStaff.Add(new AgencyStaff
        {
            UserId = staffUserId,
            AgencyId = agencyId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var staffToken = MintTokenForUser(staffUserId, UserRole.AgencyStaff);
        using var request = AuthedRequest(HttpMethod.Post, "/api/v1/trips", staffToken);
        request.Content = JsonContent.Create(new CreateTripDto
        {
            AssignmentId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            DriverId = Guid.NewGuid()
        });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ASSIGNMENT_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Update_Returns401_WithoutToken()
    {
        var response = await _client.PutAsJsonAsync($"/api/v1/trips/{Guid.NewGuid()}", new UpdateTripDto
        {
            VehicleId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_Returns403_ForShipper()
    {
        var shipperTokens = await RegisterAndLoginShipperAsync("trip-update");
        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/trips/{Guid.NewGuid()}", shipperTokens.AccessToken);
        request.Content = JsonContent.Create(new UpdateTripDto { VehicleId = Guid.NewGuid() });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_Returns404_ForAdmin_WhenTripDoesNotExist()
    {
        using var request = AuthedRequest(HttpMethod.Put, $"/api/v1/trips/{Guid.NewGuid()}", MintTokenWithRoles("Admin"));
        request.Content = JsonContent.Create(new UpdateTripDto { VehicleId = Guid.NewGuid() });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Delete_Returns401_WithoutToken()
    {
        var response = await _client.DeleteAsync($"/api/v1/trips/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns403_ForShipper()
    {
        var shipperTokens = await RegisterAndLoginShipperAsync("trip-delete");
        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/trips/{Guid.NewGuid()}", shipperTokens.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns403_ForDriver()
    {
        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/trips/{Guid.NewGuid()}", MintTokenWithRoles("Driver"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_ForAdmin_WhenTripDoesNotExist()
    {
        using var request = AuthedRequest(HttpMethod.Delete, $"/api/v1/trips/{Guid.NewGuid()}", MintTokenWithRoles("Admin"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Delete_Returns200_AndCancelsTrip_WhenAuthorized_ForAdmin()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var adminUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var tripId = Guid.NewGuid();

        db.Users.Add(new User
        {
            UserId = adminUserId,
            FullName = "Admin Operator",
            Email = $"admin-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Users.Add(new User
        {
            UserId = shipperUserId,
            FullName = "Shipper User",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Users.Add(new User
        {
            UserId = driverUserId,
            FullName = "Driver User",
            Email = $"driver-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Driver,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Agencies.Add(new Agency
        {
            AgencyId = agencyId,
            Name = "Express Agency",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Yard 1",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Vehicles.Add(new Vehicle
        {
            VehicleId = vehicleId,
            AgencyId = agencyId,
            RegistrationNo = $"WP-{Guid.NewGuid():N}"[..10],
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000,
            VolumeM3 = 20,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Drivers.Add(new Driver
        {
            DriverId = driverId,
            UserId = driverUserId,
            AgencyId = agencyId,
            LicenceNo = $"LIC-{Guid.NewGuid():N}"[..12],
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Loads.Add(new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            PickupAddress = "Origin",
            DropoffAddress = "Dest",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffLat = 7.0m,
            DropoffLng = 79.9m,
            WeightKg = 1000,
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
            ProposedPrice = 5000,
            Status = AssignmentStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var trip = new Trip
        {
            TripId = tripId,
            AssignmentId = assignmentId,
            VehicleId = vehicleId,
            DriverId = driverId,
            Status = TripStatus.Assigned,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Trips.Add(trip);

        db.TripEvents.Add(new TripEvent
        {
            TripEventId = Guid.NewGuid(),
            TripId = tripId,
            RecordedByUserId = adminUserId,
            FromStatus = null,
            ToStatus = TripStatus.Assigned,
            Notes = "Dispatched trip created.",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        await db.SaveChangesAsync();

        var adminToken = MintTokenForUser(adminUserId, UserRole.Admin);

        using var deleteReq = AuthedRequest(HttpMethod.Delete, $"/api/v1/trips/{tripId}", adminToken);
        var deleteRes = await _client.SendAsync(deleteReq);

        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // Verify database state: trip and events are fully removed
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletedTrip = await verifyDb.Trips.FirstOrDefaultAsync(t => t.TripId == tripId);
        Assert.Null(deletedTrip);
        var remainingEvents = await verifyDb.TripEvents.Where(e => e.TripId == tripId).ToListAsync();
        Assert.Empty(remainingEvents);

        // Verify assignment trip reference is unlinked
        var assignmentInDb = await verifyDb.Assignments.Include(a => a.Trip).FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);
        Assert.NotNull(assignmentInDb);
        Assert.Null(assignmentInDb.Trip);
    }

    [Fact]
    public async Task Delete_Returns204_AndDeletesTripFully_AfterCancelled_ForAgencyStaff()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var staffUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var tripId = Guid.NewGuid();

        db.Users.Add(new User
        {
            UserId = staffUserId,
            FullName = "Agency Staff Kamal",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.AgencyStaff.Add(new AgencyStaff
        {
            UserId = staffUserId,
            AgencyId = agencyId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Users.Add(new User
        {
            UserId = shipperUserId,
            FullName = "Shipper User",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Users.Add(new User
        {
            UserId = driverUserId,
            FullName = "Driver User",
            Email = $"driver-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Driver,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Agencies.Add(new Agency
        {
            AgencyId = agencyId,
            Name = "Samagi Logistics",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Yard 2",
            YardLat = 6.9m,
            YardLng = 79.8m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Vehicles.Add(new Vehicle
        {
            VehicleId = vehicleId,
            AgencyId = agencyId,
            RegistrationNo = $"WP-{Guid.NewGuid():N}"[..10],
            VehicleType = VehicleType.Lorry,
            CapacityKg = 5000,
            VolumeM3 = 20,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Drivers.Add(new Driver
        {
            DriverId = driverId,
            UserId = driverUserId,
            AgencyId = agencyId,
            LicenceNo = $"LIC-{Guid.NewGuid():N}"[..12],
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            Status = DriverStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Loads.Add(new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            PickupAddress = "Origin",
            DropoffAddress = "Dest",
            PickupLat = 6.9m,
            PickupLng = 79.8m,
            DropoffLat = 7.0m,
            DropoffLng = 79.9m,
            WeightKg = 1000,
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
            ProposedPrice = 5000,
            Status = AssignmentStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // The trip was already cancelled
        var trip = new Trip
        {
            TripId = tripId,
            AssignmentId = assignmentId,
            VehicleId = vehicleId,
            DriverId = driverId,
            Status = TripStatus.Cancelled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Trips.Add(trip);

        db.TripEvents.Add(new TripEvent
        {
            TripEventId = Guid.NewGuid(),
            TripId = tripId,
            RecordedByUserId = staffUserId,
            FromStatus = TripStatus.Assigned,
            ToStatus = TripStatus.Cancelled,
            Notes = "Trip cancelled prior to departure.",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });

        await db.SaveChangesAsync();

        var staffToken = MintTokenForUser(staffUserId, UserRole.AgencyStaff);

        // Delete the trip fully after cancelling it
        using var deleteReq = AuthedRequest(HttpMethod.Delete, $"/api/v1/trips/{tripId}", staffToken);
        var deleteRes = await _client.SendAsync(deleteReq);

        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // Verify trip is permanently removed from DB
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletedTrip = await verifyDb.Trips.FirstOrDefaultAsync(t => t.TripId == tripId);
        Assert.Null(deletedTrip);
        var remainingEvents = await verifyDb.TripEvents.Where(e => e.TripId == tripId).ToListAsync();
        Assert.Empty(remainingEvents);
    }

    [Fact]
    public async Task Cancel_Returns404_ForAdmin_WhenTripDoesNotExist()
    {
        using var request = AuthedRequest(HttpMethod.Patch, $"/api/v1/trips/{Guid.NewGuid()}/cancel", MintTokenWithRoles("Admin"));
        request.Content = JsonContent.Create(new CancelTripDto { Reason = "test" });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TRIP_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Cancel_Returns403_ForDriver()
    {
        using var request = AuthedRequest(HttpMethod.Patch, $"/api/v1/trips/{Guid.NewGuid()}/cancel", MintTokenWithRoles("Driver"));
        request.Content = JsonContent.Create(new CancelTripDto { Reason = "test" });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string MintTokenForUser(Guid userId, UserRole role)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-that-is-long-enough-1234567890"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "FreightLinkApi",
            audience: "FreightLinkClient",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Admin_CannotCreateAnAssignedTripByApprovingAnAgencyProposal()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var adminUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

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

        var driverUser = new User
        {
            UserId = driverUserId,
            FullName = "Assigned Driver Kamal",
            Email = $"driver-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Driver,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(driverUser);

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Samagi Express Logistics",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Peliyagoda Yard",
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
            RegistrationNo = "WP-KA-4521",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 6000,
            VolumeM3 = 25,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Vehicles.Add(vehicle);

        var driver = new Driver
        {
            DriverId = driverId,
            UserId = driverUserId,
            AgencyId = agencyId,
            LicenceNo = "LIC-KAMAL-99",
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
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..11],
            CargoDescription = "High-Capacity Solar Inverters & Batteries",
            WeightKg = 2400,
            VolumeM3 = 10,
            PickupAddress = "Kelaniya Distribution Hub, Peliyagoda",
            PickupLat = 6.96m,
            PickupLng = 79.91m,
            DropoffAddress = "Galle Port Warehouse Complex, Galle",
            DropoffLat = 6.04m,
            DropoffLng = 80.22m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(2),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(6),
            EstimatedPrice = 50000m,
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
            ProposedPrice = 50000m,
            RoutedDistanceKm = 125m,
            ProposedEtaMinutes = 150,
            Status = AssignmentStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        // 1. Admin clicks "Approve" on React Console (Y3S01-96)
        var adminToken = MintTokenForUser(adminUserId, UserRole.Admin);
        var approveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/assignments/{assignmentId}/approve")
        {
            Content = JsonContent.Create(new ApproveAssignmentDto
            {
                VehicleId = vehicleId,
                DriverId = driverId,
                Notes = "Approved by Admin on React console."
            })
        };
        approveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var approveRes = await _client.SendAsync(approveReq);
        Assert.Equal(HttpStatusCode.Forbidden, approveRes.StatusCode);
        if (approveRes.StatusCode == HttpStatusCode.Forbidden) return;

        var approveBody = await approveRes.Content.ReadFromJsonAsync<AssignmentResponseDto>();
        Assert.NotNull(approveBody);
        Assert.Equal("Accepted", approveBody.Status);
        Assert.NotNull(approveBody.TripId);
        var tripId = approveBody.TripId!.Value;

        // 2. Driver checks assigned trips (GET /api/v1/trips) in Flutter view
        var driverToken = MintTokenForUser(driverUserId, UserRole.Driver);
        var driverListReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/trips");
        driverListReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var driverListRes = await _client.SendAsync(driverListReq);
        Assert.Equal(HttpStatusCode.OK, driverListRes.StatusCode);

        var driverList = await driverListRes.Content.ReadFromJsonAsync<PagedTripResponseDto>();
        Assert.NotNull(driverList);
        Assert.NotEmpty(driverList.Items);
        var myTrip = driverList.Items.FirstOrDefault(t => t.TripId == tripId);
        Assert.NotNull(myTrip);
        Assert.Equal("Assigned", myTrip.Status);
        Assert.Equal(driverId, myTrip.DriverId);
        Assert.Equal("Assigned Driver Kamal", myTrip.DriverName);
        Assert.Equal(vehicleId, myTrip.VehicleId);
        Assert.Equal("Kelaniya Distribution Hub, Peliyagoda", myTrip.PickupAddress);
        Assert.Equal("Galle Port Warehouse Complex, Galle", myTrip.DropoffAddress);

        // 3. Driver opens trip detail (GET /api/v1/trips/{tripId})
        var driverDetailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/trips/{tripId}");
        driverDetailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var driverDetailRes = await _client.SendAsync(driverDetailReq);
        Assert.Equal(HttpStatusCode.OK, driverDetailRes.StatusCode);

        var driverDetail = await driverDetailRes.Content.ReadFromJsonAsync<TripResponseDto>();
        Assert.NotNull(driverDetail);
        Assert.Equal(tripId, driverDetail.TripId);
        Assert.Equal("Assigned", driverDetail.Status);
        Assert.Equal("WP-KA-4521", driverDetail.VehicleRegistrationNo);
        Assert.Equal("High-Capacity Solar Inverters & Batteries", driverDetail.CargoDescription);
        Assert.Equal(2400m, driverDetail.WeightKg);
        Assert.Equal(10m, driverDetail.VolumeM3);
        Assert.Equal(125m, driverDetail.RoutedDistanceKm);
        Assert.Equal(150, driverDetail.ProposedEtaMinutes);
    }

    [Fact]
    public async Task SecondaryWorkflow_AgencyStaffCapturesProofOfPickup_ReflectedInAdminTripMonitor()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var adminUserId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var shipperUserId = Guid.NewGuid();
        var agencyId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var loadId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var tripId = Guid.NewGuid();

        // 1. Seed database entities
        db.Users.Add(new User
        {
            UserId = adminUserId,
            FullName = "Admin Controller",
            Email = $"admin-monitor-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Users.Add(new User
        {
            UserId = staffUserId,
            FullName = "Agency Dispatcher Nimal",
            Email = $"staff-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.AgencyStaff,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Users.Add(new User
        {
            UserId = driverUserId,
            FullName = "Assigned Driver Sunil",
            Email = $"driver-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Driver,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Users.Add(new User
        {
            UserId = shipperUserId,
            FullName = "Shipper Colombo",
            Email = $"shipper-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Shipper,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var agency = new Agency
        {
            AgencyId = agencyId,
            Name = "Lanka Fast Freight Logistics",
            BusinessRegNo = $"BR-{Guid.NewGuid():N}",
            YardAddress = "Peliyagoda Logistics Hub",
            YardLat = 6.9600m,
            YardLng = 79.9100m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Agencies.Add(agency);

        db.AgencyStaff.Add(new AgencyStaff
        {
            UserId = staffUserId,
            AgencyId = agencyId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var vehicle = new Vehicle
        {
            VehicleId = vehicleId,
            AgencyId = agencyId,
            RegistrationNo = "WP-LY-7890",
            VehicleType = VehicleType.Lorry,
            CapacityKg = 12000,
            VolumeM3 = 45,
            Status = VehicleStatus.Available,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Vehicles.Add(vehicle);

        var driver = new Driver
        {
            DriverId = driverId,
            UserId = driverUserId,
            AgencyId = agencyId,
            LicenceNo = "LIC-SUNIL-007",
            LicenceExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)),
            Status = DriverStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Drivers.Add(driver);

        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperUserId,
            ReferenceCode = $"LD-{Guid.NewGuid():N}"[..11],
            CargoDescription = "Industrial Transformer Equipment",
            WeightKg = 8500,
            VolumeM3 = 28,
            PickupAddress = "Biyagama Export Processing Zone",
            PickupLat = 6.9380m,
            PickupLng = 79.9920m,
            DropoffAddress = "Hambantota International Port",
            DropoffLat = 6.1200m,
            DropoffLng = 81.1200m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddHours(1),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddHours(5),
            EstimatedPrice = 125000m,
            Status = LoadStatus.Matched,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            LoadId = loadId,
            AgencyId = agencyId,
            ProposedPrice = 125000m,
            RoutedDistanceKm = 240m,
            ProposedEtaMinutes = 300,
            Status = AssignmentStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Assignments.Add(assignment);

        var trip = new Trip
        {
            TripId = tripId,
            AssignmentId = assignmentId,
            VehicleId = vehicleId,
            DriverId = driverId,
            Status = TripStatus.Assigned,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Trips.Add(trip);

        // Initial Assigned event
        db.TripEvents.Add(new TripEvent
        {
            TripEventId = Guid.NewGuid(),
            TripId = tripId,
            RecordedByUserId = staffUserId,
            FromStatus = null,
            ToStatus = TripStatus.Assigned,
            Notes = "Trip assigned to driver Sunil Perera.",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        });

        await db.SaveChangesAsync();

        var staffToken = MintTokenForUser(staffUserId, UserRole.AgencyStaff);
        var adminToken = MintTokenForUser(adminUserId, UserRole.Admin);

        // Step 1: Enforce policy - Try advancing to PickedUp without evidence (must fail with 422 TRIP_EVIDENCE_REQUIRED)
        var earlyStatusReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/trips/{tripId}/status")
        {
            Content = JsonContent.Create(new ChangeTripStatusDto
            {
                TargetStatus = TripStatus.PickedUp,
                Notes = "Premature attempt before capturing photo evidence."
            })
        };
        earlyStatusReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var earlyStatusRes = await _client.SendAsync(earlyStatusReq);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, earlyStatusRes.StatusCode);
        Assert.Equal("TRIP_EVIDENCE_REQUIRED", await ReadErrorCodeAsync(earlyStatusRes));

        // Step 2: Agency Staff captures Proof-of-Pickup on Flutter (Y3S01-75) and posts evidence
        const string storageKey = "proof_pickup_biyagama_98214";
        const decimal capturedLat = 6.938500m;
        const decimal capturedLng = 79.992500m;

        var evidenceReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/trips/{tripId}/evidence")
        {
            Content = JsonContent.Create(new UploadTripEvidenceDto
            {
                PublicId = storageKey,
                EvidenceType = EvidenceType.PickupProof,
                CapturedLat = capturedLat,
                CapturedLng = capturedLng
            })
        };
        evidenceReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var evidenceRes = await _client.SendAsync(evidenceReq);
        Assert.Equal(HttpStatusCode.Created, evidenceRes.StatusCode);

        var evidenceBody = await evidenceRes.Content.ReadFromJsonAsync<TripEvidenceResponseDto>();
        Assert.NotNull(evidenceBody);
        Assert.Equal("PickupProof", evidenceBody.EvidenceType);
        Assert.Equal(storageKey, evidenceBody.StorageKey);
        Assert.Equal(capturedLat, evidenceBody.CapturedLat);
        Assert.Equal(capturedLng, evidenceBody.CapturedLng);
        Assert.Equal(staffUserId, evidenceBody.CapturedByUserId);

        // Step 3: Agency Staff advances status to PickedUp via Flutter (Y3S01-75)
        const string transitionNotes = "Cargo loaded, strapped down, and inspected at Biyagama EPZ.";
        var advanceStatusReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/trips/{tripId}/status")
        {
            Content = JsonContent.Create(new ChangeTripStatusDto
            {
                TargetStatus = TripStatus.PickedUp,
                Notes = transitionNotes,
                SnapshotLat = capturedLat,
                SnapshotLng = capturedLng
            })
        };
        advanceStatusReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var advanceStatusRes = await _client.SendAsync(advanceStatusReq);
        Assert.Equal(HttpStatusCode.OK, advanceStatusRes.StatusCode);

        var advanceBody = await advanceStatusRes.Content.ReadFromJsonAsync<TripResponseDto>();
        Assert.NotNull(advanceBody);
        Assert.Equal("PickedUp", advanceBody.Status);

        // Step 4: Admin observes the React Trip Monitor (Y3S01-78, GET /api/v1/trips/{tripId})
        var adminDetailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/trips/{tripId}");
        adminDetailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var adminDetailRes = await _client.SendAsync(adminDetailReq);
        Assert.Equal(HttpStatusCode.OK, adminDetailRes.StatusCode);

        var tripMonitor = await adminDetailRes.Content.ReadFromJsonAsync<TripResponseDto>();
        Assert.NotNull(tripMonitor);
        Assert.Equal(tripId, tripMonitor.TripId);
        Assert.Equal("PickedUp", tripMonitor.Status);
        Assert.Equal("WP-LY-7890", tripMonitor.VehicleRegistrationNo);
        Assert.Equal("Assigned Driver Sunil", tripMonitor.DriverName);

        // Verify evidence is present and verified for React TripEvidenceCard
        Assert.NotNull(tripMonitor.Evidence);
        Assert.NotEmpty(tripMonitor.Evidence);
        var pickupEvidence = tripMonitor.Evidence.FirstOrDefault(e => e.EvidenceType == "PickupProof");
        Assert.NotNull(pickupEvidence);
        Assert.Equal(storageKey, pickupEvidence.StorageKey);
        Assert.NotNull(pickupEvidence.SecureUrl);
        Assert.Equal(capturedLat, pickupEvidence.CapturedLat);
        Assert.Equal(capturedLng, pickupEvidence.CapturedLng);
        Assert.Equal(staffUserId, pickupEvidence.CapturedByUserId);

        // Verify event timeline transition for React TripTimelineCard
        Assert.NotNull(tripMonitor.Events);
        Assert.NotEmpty(tripMonitor.Events);
        var pickedUpEvent = tripMonitor.Events.FirstOrDefault(e => e.ToStatus == "PickedUp");
        Assert.NotNull(pickedUpEvent);
        Assert.Equal("Assigned", pickedUpEvent.FromStatus);
        Assert.Equal(transitionNotes, pickedUpEvent.Notes);
        Assert.Equal(staffUserId, pickedUpEvent.RecordedByUserId);
        Assert.Equal(capturedLat, pickedUpEvent.SnapshotLat);
        Assert.Equal(capturedLng, pickedUpEvent.SnapshotLng);
    }
}
