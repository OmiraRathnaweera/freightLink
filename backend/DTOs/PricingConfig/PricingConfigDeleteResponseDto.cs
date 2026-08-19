namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>
/// Response for <c>DELETE /api/v1/admin/pricing/fuel-rates/{id}</c> and
/// <c>DELETE /api/v1/admin/pricing/vehicle-efficiency/{id}</c> — a success confirmation only, not the
/// soft-deleted row. Fetch the corresponding <c>GET .../history</c> endpoint if the deleted row's
/// values are still needed.
/// </summary>
public class PricingConfigDeleteResponseDto
{
    /// <summary>Human-readable confirmation message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The id of the row that was soft-deleted.</summary>
    public Guid Id { get; set; }
}
