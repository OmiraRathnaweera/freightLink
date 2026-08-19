using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>
/// Payload for <c>POST /api/v1/admin/pricing/formula-config</c> — always inserts a new, current
/// <see cref="Entities.PricingFormulaConfig"/> row (append-only versioning per ADR-019); never updates an
/// existing row's values. Carries no <c>SetByUserId</c> — that comes from the authenticated Admin's
/// access-token claims in the controller, never from client input.
/// </summary>
public class CreatePricingFormulaConfigDto
{
    /// <summary>
    /// Flat base fare added to every estimate, in the project's base currency (LKR). Nullable so
    /// <see cref="RequiredAttribute"/> actually rejects an omitted JSON field — <c>0</c> is a valid,
    /// in-range value for this field, so a non-nullable <see cref="decimal"/> would let an omitted field
    /// silently bind to <c>0</c> and pass validation unnoticed (the same trap <c>CreateLoadDto.PickupLat</c>'s
    /// doc comment calls out; unlike <c>PricePerLitre</c>, this field's floor is <c>0</c>, not <c>0.01</c>).
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal? BaseFare { get; set; }

    /// <summary>Per-kilogram rate added to every estimate, in the project's base currency (LKR/kg). Nullable for the same reason as <see cref="BaseFare"/>.</summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal? RatePerKg { get; set; }

    /// <summary>
    /// Driver/maintenance/margin allowance added on top of the pure fuel-cost component of
    /// <c>ratePerKm</c>, in the project's base currency (LKR/km). Nullable for the same reason as
    /// <see cref="BaseFare"/>.
    /// </summary>
    [Required]
    [Range(0, double.MaxValue)]
    public decimal? DriverMaintenanceMarginAllowancePerKm { get; set; }

    /// <summary>Citation for where these figures came from.</summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Source { get; set; } = string.Empty;

    /// <summary>When this configuration takes effect. The current configuration is the latest non-deleted row's.</summary>
    [Required]
    public DateTimeOffset EffectiveFrom { get; set; }
}
