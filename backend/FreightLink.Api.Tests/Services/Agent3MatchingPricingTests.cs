using System.Net;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Internal;
using FreightLink.Api.DTOs.Loads;
using FreightLink.Api.DTOs.PricingConfig;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests verifying Agent 3's matching &amp; full pricing logic (ADR-012, ADR-015, ADR-019).
/// Replaces stub with real logic:
/// - Calls <see cref="IRouteService.GetRouteAndEtaAsync"/> for each candidate agency from yard to pickup.
/// - Ranks candidates by shortest positioning ETA.
/// - Calls <see cref="IRouteService.GetRouteAndEtaAsync"/> for pickup to dropoff (cargo leg).
/// - Applies the shared pricing formula: baseFare + (distanceKm * ratePerKm) + (weightKg * ratePerKg).
/// - Returns a structured recommendation proposal for the top-ranked candidate.
/// </summary>
public class Agent3MatchingPricingTests
{
    private class FakeRouteService : IRouteService
    {
        public Dictionary<(decimal, decimal, decimal, decimal), RouteEtaResponseDto> PredefinedRoutes { get; } = new();

        public Task<RouteEtaResponseDto> GetRouteEtaAsync(RouteEtaRequestDto request, CancellationToken cancellationToken = default)
        {
            var key = (request.OriginLat ?? 0m, request.OriginLng ?? 0m, request.DestinationLat ?? 0m, request.DestinationLng ?? 0m);
            if (PredefinedRoutes.TryGetValue(key, out var res))
            {
                return Task.FromResult(res);
            }
            return Task.FromResult(new RouteEtaResponseDto { Success = true, DistanceKm = 50.0m, EtaMinutes = 40 });
        }

        public Task<RouteEtaResponseDto> GetRouteAndEtaAsync(RouteEtaRequestDto request, CancellationToken cancellationToken = default)
            => GetRouteEtaAsync(request, cancellationToken);

        public Task<RouteEtaResponseDto> GetRouteAndEtaAsync(decimal originLat, decimal originLng, decimal destinationLat, decimal destinationLng, CancellationToken cancellationToken = default)
            => GetRouteEtaAsync(new RouteEtaRequestDto
            {
                OriginLat = originLat,
                OriginLng = originLng,
                DestinationLat = destinationLat,
                DestinationLng = destinationLng
            }, cancellationToken);
    }

    private class FakeEmailService : IEmailService
    {
        public Task SendAsync(FreightLink.Api.Common.Email.EmailMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendAgencyDeclinedAsync(string toEmail, string shipperName, string loadReference, string agencyName, int attemptNo, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendNewMatchFoundAsync(string toEmail, string shipperName, string loadReference, string agencyName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendNoAutomaticMatchFoundAsync(string toEmail, string shipperName, string loadReference, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        return Task.FromResult(db);
    }

    private static async Task SeedPricingConfigAsync(AppDbContext db, Guid adminUserId)
    {
        var pricingConfigService = new PricingConfigService(db);
        var effective = DateTimeOffset.UtcNow.AddMinutes(-5);

        await pricingConfigService.CreatePricingFormulaConfig(new CreatePricingFormulaConfigDto
        {
            BaseFare = 5000m,
            RatePerKg = 4m,
            DriverCostPerKm = 25m,
            MaintenanceAllowancePerKm = 15m,
            MarginPercent = 0.15m,
            Source = "Test Regulatory Gazette",
            EffectiveFrom = effective
        }, adminUserId);

        await pricingConfigService.CreateFuelPriceRate(new CreateFuelPriceRateDto
        {
            FuelType = FuelType.AutoDiesel,
            PricePerLitre = 350m,
            Source = "CPC Weekly",
            EffectiveFrom = effective
        }, adminUserId);

        await pricingConfigService.CreateVehicleClassEfficiency(new CreateVehicleClassEfficiencyDto
        {
            ClassLabel = VehicleClass.MediumLorry,
            MinPayloadKg = 0m,
            MaxPayloadKg = null,
            MinVolumeM3 = 0m,
            MaxVolumeM3 = null,
            FuelConsumptionLPer100Km = 20m,
            Source = "Manufacturer Fleet Manual",
            EffectiveFrom = effective
        }, adminUserId);
    }

    [Fact]
    public async Task GetMatchRecommendation_RanksCandidatesByShortestEta_AndPricesOnCargoLeg()
    {
        // Arrange
        var db = await CreateContextAsync();
        var adminUserId = Guid.NewGuid();
        db.Users.Add(new User
        {
            UserId = adminUserId,
            Role = UserRole.Admin,
            Email = "admin@freightlink.lk",
            PasswordHash = "hash",
            FullName = "Admin",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var shipperId = Guid.NewGuid();
        db.Users.Add(new User
        {
            UserId = shipperId,
            Role = UserRole.Shipper,
            Email = "shipper@freightlink.lk",
            PasswordHash = "hash",
            FullName = "Shipper User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await SeedPricingConfigAsync(db, adminUserId);

        // Load: Colombo to Kandy, 2500 kg
        var loadId = Guid.NewGuid();
        var load = new Load
        {
            LoadId = loadId,
            ShipperUserId = shipperId,
            ReferenceCode = "LD-TEST-001",
            PickupAddress = "Colombo Port",
            PickupLat = 6.9400m,
            PickupLng = 79.8500m,
            DropoffAddress = "Kandy Central",
            DropoffLat = 7.2900m,
            DropoffLng = 80.6300m,
            WeightKg = 2500m,
            VolumeM3 = 8m,
            Status = LoadStatus.Posted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Loads.Add(load);

        // Agency A: Yard in Galle (farther, 110 min ETA)
        var agencyA = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = "Southern Logistics",
            BusinessRegNo = "PV-0001",
            YardAddress = "Galle Port Yard",
            YardLat = 6.0300m,
            YardLng = 80.2100m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Agency B: Yard in Kelaniya (closer, 25 min ETA)
        var agencyB = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = "Metro Freight LK",
            BusinessRegNo = "PV-0002",
            YardAddress = "Kelaniya Hub",
            YardLat = 6.9600m,
            YardLng = 79.9100m,
            Status = AgencyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1), // Created later than A
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Agencies.AddRange(agencyA, agencyB);
        await db.SaveChangesAsync();

        var fakeRouteService = new FakeRouteService();

        // Agency A Yard -> Pickup: 120 km, 110 min
        fakeRouteService.PredefinedRoutes[(agencyA.YardLat, agencyA.YardLng, load.PickupLat, load.PickupLng)] =
            new RouteEtaResponseDto { Success = true, DistanceKm = 120.0m, EtaMinutes = 110 };

        // Agency B Yard -> Pickup: 14.5 km, 25 min (SHORTEST ETA WINNER)
        fakeRouteService.PredefinedRoutes[(agencyB.YardLat, agencyB.YardLng, load.PickupLat, load.PickupLng)] =
            new RouteEtaResponseDto { Success = true, DistanceKm = 14.5m, EtaMinutes = 25 };

        // Cargo Leg (Pickup -> Dropoff): 115.0 km, 195 min
        fakeRouteService.PredefinedRoutes[(load.PickupLat, load.PickupLng, load.DropoffLat, load.DropoffLng)] =
            new RouteEtaResponseDto { Success = true, DistanceKm = 115.0m, EtaMinutes = 195 };

        var pricingConfigService = new PricingConfigService(db);
        var pricingEstimator = new PricingEstimatorService(db, pricingConfigService);
        var fakeEmail = new FakeEmailService();

        var sut = new AssignmentService(db, fakeEmail, pricingEstimator, fakeRouteService);

        // Act
        var result = await sut.GetMatchRecommendationAsync(loadId, shipperId, UserRole.Shipper);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(loadId, result.LoadId);
        Assert.NotNull(result.RecommendedAgency);

        // 1. Top-ranked candidate must be Agency B (shortest ETA: 25 min vs 110 min), despite Agency A created earlier
        Assert.Equal(agencyB.AgencyId, result.RecommendedAgency.AgencyId);
        Assert.Equal("Metro Freight LK", result.RecommendedAgency.Name);
        Assert.Equal(25, result.RecommendedAgency.PositioningEtaMinutes);
        Assert.Equal(14.5m, result.RecommendedAgency.PositioningDistanceKm);

        // 2. Cargo transit leg distance is 115.0 km (from pickup to dropoff)
        Assert.Equal(115.0m, result.RecommendedAgency.CargoDistanceKm);

        // 3. Pricing uses the cargo leg distance (115.0 km) and shared pricing formula:
        // Fuel = (350 / 100) * 20 = 70. Driver = 25. Maintenance = 15. Base = 110.
        // RatePerKm = 110 * 1.15 = 126.50
        // ExpectedPrice = BaseFare (5000) + (115.0 * 126.50) + (2500 * 4) = 5000 + 14547.50 + 10000 = 29547.50
        Assert.True(result.RecommendedAgency.EstimatedPrice > 20000m);
        Assert.Equal(29547.50m, result.RecommendedAgency.EstimatedPrice);

        // 4. Alternate candidate #2 is Agency A (110 min ETA)
        Assert.Single(result.AlternateCandidates);
        var alt1 = result.AlternateCandidates[0];
        Assert.Equal(agencyA.AgencyId, alt1.AgencyId);
        Assert.Equal(2, alt1.Rank);
        Assert.Equal(110, alt1.PositioningEtaMinutes);
        Assert.Equal(120.0m, alt1.PositioningDistanceKm);
    }
}
