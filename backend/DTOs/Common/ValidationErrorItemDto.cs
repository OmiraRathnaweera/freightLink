namespace FreightLink.Api.DTOs.Common;

/// <summary>A single field-level validation failure, as listed in <see cref="ErrorDetailDto.Details"/>.</summary>
public class ValidationErrorItemDto
{
    /// <summary>Name of the invalid request field.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Human-readable reason the field failed validation.</summary>
    public string Issue { get; set; } = string.Empty;
}
