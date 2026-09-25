using System.ComponentModel.DataAnnotations;

namespace FreightLink.Api.DTOs.Trips;

/// <summary>
/// Optional payload for cancelling a Trip (<c>DELETE /api/v1/trips/{id}</c> or <c>PATCH /api/v1/trips/{id}/cancel</c>).
/// </summary>
public class CancelTripDto
{
    /// <summary>Optional reason explaining why the trip is being cancelled.</summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}
