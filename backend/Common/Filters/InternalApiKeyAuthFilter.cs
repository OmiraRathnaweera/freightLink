using System.Net;
using System.Security.Cryptography;
using System.Text;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace FreightLink.Api.Common.Filters;

/// <summary>
/// Guards internal-only, non-JWT endpoints (e.g. <c>POST /internal/pricing/estimate</c>) with a
/// shared-secret <c>X-Internal-Api-Key</c> header instead of <c>[Authorize]</c>. Applied via
/// <c>[ServiceFilter(typeof(InternalApiKeyAuthFilter))]</c> rather than
/// <c>Microsoft.AspNetCore.Authentication</c>, since this has no JWT/claims concept at all — it is a
/// caller-service-to-caller-service check, not a user identity. Throws <see cref="ApiException"/>
/// rather than writing the response directly, so the failure is caught and rendered by
/// <c>ExceptionHandlingMiddleware</c> through the exact same error envelope as every other
/// <c>ApiException</c> in this codebase.
/// </summary>
public class InternalApiKeyAuthFilter : IAsyncActionFilter
{
    private const string HeaderName = "X-Internal-Api-Key";

    private readonly InternalApiOptions _options;

    /// <summary>Creates the filter with the configured expected API key.</summary>
    public InternalApiKeyAuthFilter(IOptions<InternalApiOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var expectedKey = _options.ApiKey;
        var suppliedKey = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(expectedKey) || string.IsNullOrEmpty(suppliedKey) || !FixedTimeEquals(expectedKey, suppliedKey))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.INTERNAL_API_KEY_INVALID,
                $"A valid {HeaderName} header is required for this internal endpoint.");
        }

        await next();
    }

    /// <summary>
    /// Constant-time string comparison so a caller can't infer the correct key one character at a
    /// time from response-timing differences (a plain <c>==</c>/<c>string.Equals</c> short-circuits
    /// on the first mismatched byte).
    /// </summary>
    private static bool FixedTimeEquals(string expected, string supplied) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
}
