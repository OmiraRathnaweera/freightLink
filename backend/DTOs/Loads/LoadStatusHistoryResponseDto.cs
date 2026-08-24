namespace FreightLink.Api.DTOs.Loads;

/// <summary>
/// One recorded <see cref="Entities.Enums.LoadStatus"/> transition for a load, as included in
/// <see cref="LoadResponseDto.StatusHistory"/>. Mirrors <see cref="Entities.LoadStatusHistory"/>,
/// which is append-only at the database level (see that entity's remarks).
/// </summary>
public class LoadStatusHistoryResponseDto
{
    /// <summary>Primary key of this history row.</summary>
    public Guid LoadStatusHistoryId { get; set; }

    /// <summary>Status before the transition, or <see langword="null"/> for the load's very first status row.</summary>
    public string? FromStatus { get; set; }

    /// <summary>Status after the transition.</summary>
    public string ToStatus { get; set; } = string.Empty;

    /// <summary>The reason given for the transition, if one was required (e.g. cancellation).</summary>
    public string? Reason { get; set; }

    /// <summary>The id of the user who triggered the transition.</summary>
    public Guid ChangedByUserId { get; set; }

    /// <summary>When the transition was recorded.</summary>
    public DateTimeOffset ChangedAt { get; set; }
}
