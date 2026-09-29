using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

/// <summary>
/// Full-HTTP-pipeline tests for <c>InternalPricingController</c>, focused on the
/// <c>X-Internal-Api-Key</c> header gate (no JWT involved) and the estimate computation's error
/// cases. Each test builds its own short-lived <see cref="CustomWebApplicationFactory"/> (own
/// InMemory database), mirroring <c>AdminPricingControllerTests</c>' pattern — some tests here
/// deliberately remove the factory's default pricing-config seed rows to exercise the
/// "no current config" 503 cases, which would spuriously interfere with other tests sharing one
/// database. Service-level arithmetic/concurrency coverage lives in <c>PricingEstimatorServiceTests</c>.
/// </summary>
public class InternalPricingControllerTests
{
    private const string ValidKey = CustomWebApplicationFactory.ValidInternalApiKey;

    /// <summary>Registers a new shipper with a unique email and logs in, returning the issued tokens.</summary>
    private static async Task<TokenResponseDto> RegisterAndLoginShipperAsync(HttpClient client, string emailPrefix)
    {
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3r$ecret1";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register/shipper", new RegisterShipperRequestDto
        {
            Email = email,
            Password = password,
            FullName = "Integration Tester",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        return (await loginResponse.Content.ReadFromJsonAsync<TokenResponseDto>())!;
    }

    /// <summary>Registers/logs in a shipper and creates a load via the public API, returning the created load.</summary>
    private static async Task<LoadResponseDto> SeedLoadAsync(HttpClient client)
    {
        var tokens = await RegisterAndLoginShipperAsync(client, "internal-pricing-shipper");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/loads");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Content = JsonContent.Create(new CreateLoadDto
        {
            CargoDescription = "Pallets of canned goods",
            WeightKg = 500m,
            VolumeM3 = 2.5m,
            PickupAddress = "123 Pickup Street, Colombo",
            PickupLat = 6.9271m,
            PickupLng = 79.8612m,
            DropoffAddress = "456 Dropoff Road, Kandy",
            DropoffLat = 7.2906m,
            DropoffLng = 80.6337m,
            PickupWindowStart = DateTimeOffset.UtcNow.AddDays(1),
            PickupWindowEnd = DateTimeOffset.UtcNow.AddDays(2)
        });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoadResponseDto>())!;
    }

    private static HttpRequestMessage EstimateRequest(Guid loadId, string? apiKey, VehicleClass vehicleClass = VehicleClass.MiniTruck, decimal distanceKm = 100m)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/pricing/estimate")
        {
            Content = JsonContent.Create(new EstimatePricingRequestDto
            {
                LoadId = loadId,
                SuggestedVehicleClass = vehicleClass,
                DistanceKm = distanceKm
            })
        };
        if (apiKey is not null)
        {
            request.Headers.Add("X-Internal-Api-Key", apiKey);
        }
        return request;
    }

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        return json.RootElement.GetProperty("error").GetProperty("code").GetString()!;
    }

    /// <summary>Removes the factory's seeded rows for one pricing-config table, to exercise the "no current config" 503 case.</summary>
    private static async Task RemoveSeededPricingDataAsync(CustomWebApplicationFactory factory, string table)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (table)
        {
            case "FuelPriceRates":
                dbContext.FuelPriceRates.RemoveRange(dbContext.FuelPriceRates);
                break;
            case "PricingFormulaConfigs":
                dbContext.PricingFormulaConfigs.RemoveRange(dbContext.PricingFormulaConfigs);
                break;
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>Correct key + valid payload succeeds, returns the full breakdown, and persists Load.EstimatedPrice.</summary>
    [Fact]
    public async Task Estimate_Returns200_WithBreakdown_AndPersistsEstimatedPrice()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var load = await SeedLoadAsync(client);

        using var request = EstimateRequest(load.LoadId, ValidKey);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PricingEstimateResponseDto>();
        Assert.Equal(load.LoadId, result!.LoadId);
        Assert.True(result.EstimatedPrice > 0);

        // Verify persistence via a direct DB read instead of a shipper GET (out of scope for this
        // header-only-guarded controller's tests) — mirrors how PricingEstimatorServiceTests checks this.
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await dbContext.Loads.AsNoTracking().SingleAsync(l => l.LoadId == load.LoadId);
        Assert.Equal(result.EstimatedPrice, row.EstimatedPrice);
    }

    /// <summary>A wrong X-Internal-Api-Key header is 401.</summary>
    [Fact]
    public async Task Estimate_Returns401_WhenKeyWrong()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var load = await SeedLoadAsync(client);

        using var request = EstimateRequest(load.LoadId, apiKey: "not-the-right-key");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INTERNAL_API_KEY_INVALID", await ReadErrorCodeAsync(response));
    }

    /// <summary>A missing loadId fails DataAnnotations validation automatically — 400 VALIDATION_ERROR.</summary>
    [Fact]
    public async Task Estimate_Returns400_WhenLoadIdMissing()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/pricing/estimate")
        {
            Content = JsonContent.Create(new { suggestedVehicleClass = "MiniTruck", distanceKm = 42.3m })
        };
        request.Headers.Add("X-Internal-Api-Key", ValidKey);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCodeAsync(response));
    }

    /// <summary>An invalid vehicle class string fails enum-string deserialization/validation — 400 VALIDATION_ERROR.</summary>
    [Fact]
    public async Task Estimate_Returns400_WhenVehicleClassInvalid()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var load = await SeedLoadAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/pricing/estimate")
        {
            Content = JsonContent.Create(new { loadId = load.LoadId, suggestedVehicleClass = "NotARealClass", distanceKm = 42.3m })
        };
        request.Headers.Add("X-Internal-Api-Key", ValidKey);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A nonexistent loadId is 404.</summary>
    [Fact]
    public async Task Estimate_Returns404_WhenLoadNotFound()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var request = EstimateRequest(Guid.NewGuid(), ValidKey);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("LOAD_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    /// <summary>No current fuel price configured is a 503, not a 500 or a silent hardcoded fallback.</summary>
    [Fact]
    public async Task Estimate_Returns503_WhenFuelPriceMissing()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var load = await SeedLoadAsync(client);
        await RemoveSeededPricingDataAsync(factory, "FuelPriceRates");

        using var request = EstimateRequest(load.LoadId, ValidKey);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("PRICING_CONFIG_MISSING", await ReadErrorCodeAsync(response));
    }

    /// <summary>No current efficiency figure for the requested vehicle class is a 503 — the factory only seeds MiniTruck.</summary>
    [Fact]
    public async Task Estimate_Returns503_WhenVehicleClassEfficiencyMissing()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var load = await SeedLoadAsync(client);

        using var request = EstimateRequest(load.LoadId, ValidKey, vehicleClass: VehicleClass.ContainerTruck);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("PRICING_CONFIG_MISSING", await ReadErrorCodeAsync(response));
    }

}
