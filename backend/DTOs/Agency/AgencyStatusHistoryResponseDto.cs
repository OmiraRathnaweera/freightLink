using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.DTOs.Agency;

/// <summary>One entry in an agency's status audit trail: <c>GET /agencies/{id}/status-history</c>.</summary>
public class AgencyStatusHistoryResponseDto
{
    public Guid AgencyStatusHistoryId { get; set; }

    /// <summary>Status before the change; null for the agency's first row (and for rows written before from-status was recorded).</summary>
    public AgencyStatus? FromStatus { get; set; }

    public AgencyStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public Guid ChangedByUserId { get; set; }

    /// <summary>Full name of the user who made the change, when that user still resolves.</summary>
    public string? ChangedByName { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
