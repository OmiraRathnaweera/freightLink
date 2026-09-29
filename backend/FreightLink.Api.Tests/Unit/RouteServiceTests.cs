using System.Net;
using System.Text;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

public class RouteServiceTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(_responseFactory(request));
        }
    }

    private class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private static IConfiguration CreateConfig(string? apiKey = "test-ors-api-key", string? baseUrl = "https://api.openrouteservice.org")
    {
        var dict = new Dictionary<string, string?>();
        if (apiKey != null) dict["OpenRouteService:ApiKey"] = apiKey;
        if (baseUrl != null) dict["OpenRouteService:BaseUrl"] = baseUrl;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task GetRouteEtaAsync_MissingCoordinates_ReturnsSuccessFalse()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var sut = new RouteService(httpClient, CreateConfig(), new NullLogger<RouteService>());

        var request = new RouteEtaRequestDto
        {
            OriginLat = null,
            OriginLng = 79.8612m,
            DestinationLat = 7.2906m,
            DestinationLng = 80.6337m
        };

        var result = await sut.GetRouteEtaAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.DistanceKm);
        Assert.Null(result.EtaMinutes);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetRouteEtaAsync_NoApiKeyConfigured_UsesFallbackCalculations()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var config = CreateConfig(apiKey: "");
        var sut = new RouteService(httpClient, config, new NullLogger<RouteService>());

        var request = new RouteEtaRequestDto
        {
            OriginLat = 6.9344m, // Colombo
            OriginLng = 79.8428m,
            DestinationLat = 7.2906m, // Kandy
            DestinationLng = 80.6337m
        };

        var result = await sut.GetRouteEtaAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.DistanceKm);
        Assert.True(result.DistanceKm > 90m);
        Assert.NotNull(result.EtaMinutes);
        Assert.True(result.EtaMinutes > 60);
        Assert.Equal(0, handler.CallCount); // Did not make HTTP call
    }

    [Fact]
    public async Task GetRouteEtaAsync_SuccessfulLiveApiResponse_ParsesGeoJsonCorrectly()
    {
        const string mockGeoJson = """
        {
            "type": "FeatureCollection",
            "features": [
                {
                    "type": "Feature",
                    "properties": {
                        "summary": {
                            "distance": 115400.0,
                            "duration": 7200.0
                        }
                    }
                }
            ]
        }
        """;

        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(mockGeoJson, Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler);
        var sut = new RouteService(httpClient, CreateConfig(), new NullLogger<RouteService>());

        var request = new RouteEtaRequestDto
        {
            OriginLat = 6.9271m,
            OriginLng = 79.8612m,
            DestinationLat = 7.2906m,
            DestinationLng = 80.6337m
        };

        var result = await sut.GetRouteEtaAsync(request);

        Assert.True(result.Success);
        Assert.Equal(115.4m, result.DistanceKm);
        Assert.Equal(120, result.EtaMinutes); // 7200s = 120min
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("test-ors-api-key", handler.LastRequest?.Headers.GetValues("Authorization").First());
    }

    [Fact]
    public async Task GetRouteEtaAsync_RetriesOnTransientFailure_AndSucceedsOnSecondAttempt()
    {
        const string mockGeoJson = """
        {
            "type": "FeatureCollection",
            "features": [
                {
                    "type": "Feature",
                    "properties": {
                        "summary": {
                            "distance": 50000.0,
                            "duration": 3600.0
                        }
                    }
                }
            ]
        }
        """;

        var attempt = 0;
        var handler = new TestHttpMessageHandler(_ =>
        {
            attempt++;
            if (attempt == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(mockGeoJson, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var sut = new RouteService(httpClient, CreateConfig(), new NullLogger<RouteService>());

        var request = new RouteEtaRequestDto
        {
            OriginLat = 6.9271m,
            OriginLng = 79.8612m,
            DestinationLat = 6.9667m,
            DestinationLng = 79.8917m
        };

        var result = await sut.GetRouteEtaAsync(request);

        Assert.True(result.Success);
        Assert.Equal(50.0m, result.DistanceKm);
        Assert.Equal(60, result.EtaMinutes);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetRouteEtaAsync_PermanentFailure_ReturnsSuccessFalseWithoutThrowing()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var httpClient = new HttpClient(handler);
        var sut = new RouteService(httpClient, CreateConfig(), new NullLogger<RouteService>());

        var request = new RouteEtaRequestDto
        {
            OriginLat = 6.9271m,
            OriginLng = 79.8612m,
            DestinationLat = 7.2906m,
            DestinationLng = 80.6337m
        };

        var result = await sut.GetRouteEtaAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.DistanceKm);
        Assert.Null(result.EtaMinutes);
        Assert.Equal(2, handler.CallCount); // Tried twice (1 retry)
    }

    [Fact]
    public async Task GetRouteEtaAsync_RepeatedRoute_ServedFromMemoryCache()
    {
        const string mockGeoJson = """
        {
            "type": "FeatureCollection",
            "features": [
                {
                    "type": "Feature",
                    "properties": {
                        "summary": {
                            "distance": 80000.0,
                            "duration": 4800.0
                        }
                    }
                }
            ]
        }
        """;

        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(mockGeoJson, Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new RouteService(httpClient, CreateConfig(), new NullLogger<RouteService>(), cache);

        var request = new RouteEtaRequestDto
        {
            OriginLat = 6.9271m,
            OriginLng = 79.8612m,
            DestinationLat = 7.2906m,
            DestinationLng = 80.6337m
        };

        // First call populates cache
        var result1 = await sut.GetRouteEtaAsync(request);
        Assert.True(result1.Success);
        Assert.Equal(1, handler.CallCount);

        // Second call with same coordinates hits cache
        var result2 = await sut.GetRouteEtaAsync(request);
        Assert.True(result2.Success);
        Assert.Equal(result1.DistanceKm, result2.DistanceKm);
        Assert.Equal(1, handler.CallCount); // Still 1! Cache served
    }
}
