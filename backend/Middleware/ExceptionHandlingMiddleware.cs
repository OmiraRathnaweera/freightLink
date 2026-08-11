using System.Net;
using System.Text.Json;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Common;

namespace FreightLink.Api.Middleware;

/// <summary>
/// Wraps the request pipeline and converts exceptions into the standard error envelope
/// (<c>{ "error": { code, message, details } }</c>): <see cref="ApiException"/> is mapped to its
/// own status/code/message, and any other unhandled exception becomes a logged 500.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>Creates the middleware with the next delegate in the pipeline and a logger for unhandled exceptions.</summary>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Invokes the rest of the pipeline, catching and translating any exception into the error envelope.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException apiException)
        {
            await WriteErrorAsync(
                context,
                (int)apiException.StatusCode,
                new ErrorDetailDto
                {
                    Code = apiException.Code.ToString(),
                    Message = apiException.Message,
                    Details = apiException.Details
                });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);

            await WriteErrorAsync(
                context,
                (int)HttpStatusCode.InternalServerError,
                new ErrorDetailDto
                {
                    Code = ErrorCode.INTERNAL_SERVER_ERROR.ToString(),
                    Message = "An unexpected error occurred."
                });
        }
    }

    /// <summary>Writes a JSON error envelope with the given status code to the response.</summary>
    private static Task WriteErrorAsync(HttpContext context, int statusCode, ErrorDetailDto error)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var envelope = new ErrorEnvelopeDto(error);
        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return context.Response.WriteAsync(json);
    }
}
