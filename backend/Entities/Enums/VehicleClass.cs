namespace FreightLink.Api.Entities.Enums;

/// <summary>
/// Payload-weight tiers a <see cref="Entities.VehicleClassEfficiency"/> row prices, per ADR-019.
/// Deliberately decoupled from <c>Vehicle.VehicleType</c> — see that entity's remarks.
/// </summary>
public enum VehicleClass
{
    MiniTruck,
    MediumLorry,
    ContainerTruck
}
