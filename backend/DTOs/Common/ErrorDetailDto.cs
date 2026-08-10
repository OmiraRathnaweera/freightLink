namespace FreightLink.Api.DTOs.Common;

/// <summary>The "error" payload of the standard API error envelope (see <see cref="ErrorEnvelopeDto"/>).</summary>
public class ErrorDetailDto
{
    /// <summary>Machine-readable error code — the string form of a <see cref="FreightLink.Api.Common.Errors.ErrorCode"/> member, e.g. "VALIDATION_ERROR" or "INVALID_CREDENTIALS".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable summary of the error.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Field-level validation failures, present only for validation errors.</summary>
    public List<ValidationErrorItemDto>? Details { get; set; }
}
