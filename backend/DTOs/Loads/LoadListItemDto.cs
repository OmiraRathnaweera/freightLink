namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// Lightweight row shape for <c>GET /api/v1/loads</c> list results. Omits fields not needed for a
/// list view (lat/lng, volume, owner id, workflow run id) — see <see cref="LoadResponseDto"/> for the
/// full detail shape.
/// </summary>
public class LoadListItemDto
{
    /// <summary>The load's unique id.</summary>
    public Guid LoadId { get; set; }

    /// <summary>
    /// Display name (<see cref="Entities.User.FullName"/>) of the Shipper user who owns this load, for
    /// Admin views that list loads across multiple shippers. Falls back to <c>"Unknown"</c> if the
    /// owning user record could not be resolved.
    /// </summary>
    public string ShipperName { get; set; } = string.Empty;

    /// <summary>Server-generated, unique reference code for this load.</summary>
    public string ReferenceCode { get; set; } = string.Empty;

    /// <summary>Free-text description of the cargo being shipped.</summary>
    public string CargoDescription { get; set; } = string.Empty;

    /// <summary>Cargo weight in kilograms.</summary>
    public decimal WeightKg { get; set; }

    /// <summary>Human-readable pickup address.</summary>
    public string PickupAddress { get; set; } = string.Empty;

    /// <summary>Human-readable dropoff address.</summary>
    public string DropoffAddress { get; set; } = string.Empty;

    /// <summary>Start of the pickup window.</summary>
    public DateTimeOffset PickupWindowStart { get; set; }

    /// <summary>End of the pickup window.</summary>
    public DateTimeOffset PickupWindowEnd { get; set; }

    /// <summary>System-computed price estimate, if one has been generated via the estimate endpoint.</summary>
    public decimal? EstimatedPrice { get; set; }

    /// <summary>The load's current status (e.g. "Draft", "Posted", "Matched").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the load was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
