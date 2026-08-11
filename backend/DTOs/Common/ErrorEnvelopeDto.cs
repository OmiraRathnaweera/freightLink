namespace FreightLink.Api.DTOs.Common;

/// <summary>
/// The top-level shape every non-2xx API response must use: <c>{ "error": { code, message, details } }</c>.
/// Written by <see cref="FreightLink.Api.Middleware.ExceptionHandlingMiddleware"/> and the
/// <c>InvalidModelStateResponseFactory</c> override in <c>Program.cs</c>.
/// </summary>
public class ErrorEnvelopeDto
{
    /// <summary>Creates an envelope wrapping the given error detail.</summary>
    /// <param name="error">The error detail to wrap.</param>
    public ErrorEnvelopeDto(ErrorDetailDto error)
    {
        Error = error;
    }

    /// <summary>The wrapped error detail.</summary>
    public ErrorDetailDto Error { get; set; }
}
