using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.PricingConfig;

/// <summary>Wire-facing representation of a <see cref="Entities.FuelPriceRate"/> row.</summary>
public class FuelPriceRateResponseDto
{
    /// <summary>The row's id.</summary>
    public Guid FuelPriceRateId { get; set; }

    /// <summary>The fuel grade this rate prices.</summary>
    public FuelType FuelType { get; set; }

    /// <summary>Price per litre, in the project's base currency.</summary>
    public decimal PricePerLitre { get; set; }

    /// <summary>Citation for where this price came from.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When this price took/takes effect.</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>The id of the Admin who recorded this rate.</summary>
    public Guid SetByUserId { get; set; }

    /// <summary>
    /// Display name (<see cref="Entities.User.FullName"/>) of the Admin who recorded this rate. Falls
    /// back to <c>"Unknown"</c> if the setting user record could not be resolved.
    /// </summary>
    public string SetByUserName { get; set; } = string.Empty;

    /// <summary>Timestamp the row was inserted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Soft-delete timestamp; null unless this row has been soft-deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>The id of the Admin who soft-deleted this row; null unless this row has been soft-deleted.</summary>
    public Guid? DeletedByUserId { get; set; }
}
