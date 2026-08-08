namespace FreightLink.Api.Entities.Enums;

public enum TripStatus
{
    Created,
    EnRouteToPickup,
    ArrivedAtPickup,
    Loaded,
    InTransit,
    ArrivedAtDropoff,
    Delivered,
    Cancelled
}
