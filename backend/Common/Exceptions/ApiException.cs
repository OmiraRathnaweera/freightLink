using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.DTOs.Common;

namespace FreightLink.Api.Common.Exceptions;

/// <summary>
/// Thrown by services to signal a business-rule/auth failure that should surface as a specific
/// HTTP status and error code. Caught by <see cref="FreightLink.Api.Middleware.ExceptionHandlingMiddleware"/>
/// and mapped straight to the standard error envelope.
/// </summary>
public class ApiException : Exception
{
    /// <summary>Creates an API exception carrying the HTTP status, error code, and message to return to the caller.</summary>
    /// <param name="statusCode">HTTP status code the middleware should write.</param>
    /// <param name="code">Machine-readable error code, e.g. <see cref="ErrorCode.INVALID_CREDENTIALS"/>.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="details">Optional field-level validation failures.</param>
    public ApiException(HttpStatusCode statusCode, ErrorCode code, string message, List<ValidationErrorItemDto>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    /// <summary>HTTP status code the middleware should write for this failure.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Machine-readable error code included in the error envelope.</summary>
    public ErrorCode Code { get; }

    /// <summary>Optional field-level validation failures included in the error envelope.</summary>
    public List<ValidationErrorItemDto>? Details { get; }
}
