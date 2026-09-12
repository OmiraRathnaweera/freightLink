using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>
/// Payload for <c>POST /api/v1/admin/pricing/fuel-rates</c> — always inserts a new, current
/// <see cref="Entities.FuelPriceRate"/> row (append-only versioning per ADR-019); never updates an
/// existing row's values. Carries no <c>SetByUserId</c> — that comes from the authenticated Admin's
/// access-token claims in the controller, never from client input.
/// </summary>
public class CreateFuelPriceRateDto
{
    /// <summary>
    /// The fuel grade this rate prices. Nullable so <see cref="RequiredAttribute"/> actually rejects
    /// an omitted JSON field instead of silently binding it to the enum's default member (mirrors why
    /// coordinate fields on <c>CreateLoadDto</c> are nullable).
    /// </summary>
    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FuelType? FuelType { get; set; }

    /// <summary>Price per litre, in the project's base currency. Must be greater than zero, mirroring the DB's <c>ck_fpr_price_positive</c> CHECK.</summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "PricePerLitre must be greater than zero")]
    public decimal PricePerLitre { get; set; }

    /// <summary>Citation for where this price came from, e.g. "CPC official price list, ceypetco.gov.lk, Aug 2026".</summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Source { get; set; } = string.Empty;

    /// <summary>When this price takes effect. The current price is the latest non-deleted row's.</summary>
    [Required]
    public DateTimeOffset EffectiveFrom { get; set; }
}
