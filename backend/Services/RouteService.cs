using System.Text;
using System.Text.Json;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace FreightLink.Api.Services;

/// <summary>
/// Production implementation of <see cref="IRouteService"/> wrapping the live OpenRouteService Directions API.
/// Computes real-world driving distance and travel time between two coordinates.
///
/// <para>
/// <b>Reliability & Graceful Degradation:</b>
/// Implements the "one retry on failure/timeout, then return <c>Success = false</c>" reliability rule.
/// Rate limits (HTTP 429), timeouts, and upstream errors never throw unhandled exceptions.
/// If no API key is configured or a placeholder is present, falls back to deterministic road network
/// distance approximation using the Haversine formula with Sri Lankan road network detour factors.
/// </para>
///
/// <para>
/// <b>Caching:</b>
/// Caches successful route calculations via <see cref="IMemoryCache"/> based on coordinate bounding
/// (~11m precision) to respect OpenRouteService free-tier quota limits.
/// </para>
/// </summary>
public class RouteService : IRouteService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache? _cache;
    private readonly ILogger<RouteService> _logger;
    private readonly string? _apiKey;
    private readonly string _baseUrl;

    private const int MaxAttempts = 2; // Initial attempt + 1 retry
    private const double RoadDetourFactor = 1.25; // Sri Lankan road network curvature factor
    private const double AverageFreightSpeedKmh = 40.0; // Average commercial freight speed (km/h)

    public RouteService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RouteService> logger,
        IMemoryCache? cache = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;

        _apiKey = configuration["OpenRouteService:ApiKey"]
            ?? configuration["OPENROUTESERVICE__APIKEY"]
            ?? Environment.GetEnvironmentVariable("OPENROUTESERVICE__APIKEY");

        _baseUrl = (configuration["OpenRouteService:BaseUrl"]
            ?? configuration["OPENROUTESERVICE__BASEURL"]
            ?? Environment.GetEnvironmentVariable("OPENROUTESERVICE__BASEURL")
            ?? "https://api.openrouteservice.org").TrimEnd('/');

        if (_httpClient.Timeout == TimeSpan.FromSeconds(100))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }
    }

    /// <inheritdoc />
    public async Task<RouteEtaResponseDto> GetRouteEtaAsync(RouteEtaRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.OriginLat is null || request.OriginLng is null ||
            request.DestinationLat is null || request.DestinationLng is null)
        {
            _logger.LogWarning("GetRouteEtaAsync called with missing coordinate parameters.");
            return new RouteEtaResponseDto { Success = false };
        }

        var origLat = request.OriginLat.Value;
        var origLng = request.OriginLng.Value;
        var destLat = request.DestinationLat.Value;
        var destLng = request.DestinationLng.Value;

        // Check in-memory cache to conserve ORS request quota
        var cacheKey = $"ors:{Math.Round(origLat, 4)}:{Math.Round(origLng, 4)}:{Math.Round(destLat, 4)}:{Math.Round(destLng, 4)}";
        if (_cache != null && _cache.TryGetValue(cacheKey, out RouteEtaResponseDto? cached) && cached != null)
        {
            _logger.LogDebug("Cache hit for route [{OrigLat}, {OrigLng}] -> [{DestLat}, {DestLng}]", origLat, origLng, destLat, destLng);
            return cached;
        }

        // Fallback for offline dev/test when no valid ORS API key is supplied
        var apiKey = _apiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("your-", StringComparison.OrdinalIgnoreCase) || apiKey.Equals("placeholder", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("No real OpenRouteService API key configured; calculating via Haversine road detour fallback.");
            var fallback = CalculateFallback(origLat, origLng, destLat, destLng);
            var fallbackResult = new RouteEtaResponseDto
            {
                Success = true,
                DistanceKm = fallback.DistanceKm,
                EtaMinutes = fallback.EtaMinutes
            };
            _cache?.Set(cacheKey, fallbackResult, TimeSpan.FromHours(24));
            return fallbackResult;
        }

        // Call live OpenRouteService Directions API with 1 retry on failure
        var url = $"{_baseUrl}/v2/directions/driving-car/geojson";
        var requestBody = new
        {
            coordinates = new[]
            {
                new[] { origLng, origLat }, // ORS expects [lng, lat]
                new[] { destLng, destLat }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.TryAddWithoutValidation("Authorization", apiKey);
                httpRequest.Headers.TryAddWithoutValidation("Accept", "application/geo+json, application/json, */*");

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(responseJson);

                    if (doc.RootElement.TryGetProperty("features", out var features) && features.GetArrayLength() > 0)
                    {
                        var summary = features[0].GetProperty("properties").GetProperty("summary");
                        var distanceMeters = summary.GetProperty("distance").GetDecimal();
                        var durationSeconds = summary.GetProperty("duration").GetDecimal();

                        var distanceKm = Math.Round(distanceMeters / 1000m, 2);
                        var etaMinutes = Math.Max(1, (int)Math.Round(durationSeconds / 60m));

                        var successResult = new RouteEtaResponseDto
                        {
                            Success = true,
                            DistanceKm = distanceKm,
                            EtaMinutes = etaMinutes
                        };

                        _cache?.Set(cacheKey, successResult, TimeSpan.FromHours(24));
                        return successResult;
                    }

                    _logger.LogWarning("OpenRouteService returned no features in route response.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("OpenRouteService HTTP {StatusCode} on attempt {Attempt}: {Error}",
                        (int)response.StatusCode, attempt, errorContent);

                    if (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("OpenRouteService call attempt {Attempt} non-success, retrying once with short backoff...", attempt);
                        await Task.Delay(300 * attempt, cancellationToken);
                        continue;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "OpenRouteService call attempt {Attempt} failed/timed out, retrying once with short backoff...", attempt);
                await Task.Delay(300 * attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenRouteService call failed permanently after {Attempt} attempt(s). Entering safe hold for review state.", attempt);
            }
        }

        // Returning in-band failure per reliability rule (never throws): safe "hold for review"
        _logger.LogWarning("OpenRouteService permanently unavailable after retries. Yielding in-band failure for safe hold for review state.");
        return new RouteEtaResponseDto
        {
            Success = false,
            DistanceKm = null,
            EtaMinutes = null
        };
    }

    private static (decimal DistanceKm, int EtaMinutes) CalculateFallback(
        decimal originLat, decimal originLng, decimal destLat, decimal destLng)
    {
        const double r = 6371.0;
        var lat1 = (double)originLat * Math.PI / 180.0;
        var lat2 = (double)destLat * Math.PI / 180.0;
        var dLat = ((double)destLat - (double)originLat) * Math.PI / 180.0;
        var dLng = ((double)destLng - (double)originLng) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLng / 2.0) * Math.Sin(dLng / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        var haversineKm = r * c;

        var roadDistanceKm = (decimal)Math.Round(Math.Max(0.5, haversineKm * RoadDetourFactor), 2);
        var etaMinutes = Math.Max(5, (int)Math.Round(((double)roadDistanceKm / AverageFreightSpeedKmh) * 60.0));
        return (roadDistanceKm, etaMinutes);
    }

    /// <inheritdoc />
    public Task<RouteEtaResponseDto> GetRouteAndEtaAsync(RouteEtaRequestDto request, CancellationToken cancellationToken = default)
        => GetRouteEtaAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<RouteEtaResponseDto> GetRouteAndEtaAsync(decimal originLat, decimal originLng, decimal destinationLat, decimal destinationLng, CancellationToken cancellationToken = default)
        => GetRouteEtaAsync(new RouteEtaRequestDto
        {
            OriginLat = originLat,
            OriginLng = originLng,
            DestinationLat = destinationLat,
            DestinationLng = destinationLng
        }, cancellationToken);
}