using System.Net;
using System.Net.Http.Json;
using FreightLink.Api.DTOs.Internal;
using Xunit;

namespace FreightLink.Api.Tests.Integration;

public class RoutingControllerTests
{
    private const string ValidKey = CustomWebApplicationFactory.ValidInternalApiKey;

    [Fact]
    public async Task GetRouteEta_WithoutInternalApiKey_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var payload = new RouteEtaRequestDto
        {
            OriginLat = 6.9271m,
            OriginLng = 79.8612m,
            DestinationLat = 7.2906m,
            DestinationLng = 80.6337m
        };

        var response = await client.PostAsJsonAsync("/internal/routing/route-eta", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRouteEta_WithInvalidInternalApiKey_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/routing/route-eta");
        request.Headers.Add("X-Internal-Api-Key", "wrong-secret");
        request.Content = JsonContent.Create(new RouteEtaRequestDto
        {
            OriginLat = 6.9271m,
            OriginLng = 79.8612m,
            DestinationLat = 7.2906m,
            DestinationLng = 80.6337m
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRouteEta_WithValidKeyAndValidCoordinates_Returns200WithDistanceAndEta()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/routing/route-eta");
        request.Headers.Add("X-Internal-Api-Key", ValidKey);
        request.Content = JsonContent.Create(new RouteEtaRequestDto
        {
            OriginLat = 6.9271m,
            OriginLng = 79.8612m,
            DestinationLat = 7.2906m,
            DestinationLng = 80.6337m
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RouteEtaResponseDto>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.DistanceKm);
        Assert.True(result.DistanceKm > 0);
        Assert.NotNull(result.EtaMinutes);
        Assert.True(result.EtaMinutes > 0);
    }

    [Fact]
    public async Task GetRouteEta_WithInvalidCoordinates_Returns400BadRequest()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/routing/route-eta");
        request.Headers.Add("X-Internal-Api-Key", ValidKey);
        request.Content = JsonContent.Create(new RouteEtaRequestDto
        {
            OriginLat = 999.0m, // Invalid latitude out of [-90, 90] bounds
            OriginLng = 79.8612m,
            DestinationLat = 7.2906m,
            DestinationLng = 80.6337m
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
