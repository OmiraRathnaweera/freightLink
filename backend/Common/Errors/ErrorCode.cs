namespace FreightLink.Api.Common.Errors;

/// <summary>
/// Every machine-readable error code the API can return in the standard error envelope
/// (<c>{ "error": { "code", "message", "details" } }</c>). Members are named in
/// <c>SCREAMING_SNAKE_CASE</c> to match the wire value exactly, so <c>.ToString()</c>
/// reproduces the JSON <c>code</c> field with no extra mapping step.
/// </summary>
public enum ErrorCode
{
    /// <summary>DataAnnotations model validation failed on the request body.</summary>
    VALIDATION_ERROR,

    /// <summary>Login failed because the email is unknown or the password doesn't match (never disambiguated, to avoid user enumeration).</summary>
    INVALID_CREDENTIALS,

    /// <summary>Credentials were correct but the account is marked inactive.</summary>
    ACCOUNT_INACTIVE,

    /// <summary>Registration was attempted with an email that already has an account.</summary>
    EMAIL_ALREADY_REGISTERED,

    /// <summary>Agency registration was attempted with a business registration number already on file.</summary>
    BUSINESS_REG_NO_ALREADY_REGISTERED,

    /// <summary>The supplied refresh token is unknown, revoked, or expired.</summary>
    INVALID_REFRESH_TOKEN,

    /// <summary>The supplied refresh token is valid but does not belong to the authenticated caller.</summary>
    REFRESH_TOKEN_NOT_OWNED,

    /// <summary>The authenticated caller's user record could not be found.</summary>
    USER_NOT_FOUND,

    /// <summary>The requested load could not be found.</summary>
    LOAD_NOT_FOUND,

    /// <summary>A load's <c>PickupWindowEnd</c> was not after its <c>PickupWindowStart</c>.</summary>
    INVALID_PICKUP_WINDOW,

    /// <summary>An edit or cancel was attempted on a load whose current status doesn't allow it.</summary>
    INVALID_LOAD_STATUS_TRANSITION,

    /// <summary>A load cancellation was attempted without a reason.</summary>
    LOAD_CANCEL_REASON_REQUIRED,

    /// <summary>The server-generated load reference code collided with an existing one.</summary>
    LOAD_REFERENCE_CODE_CONFLICT,

    /// <summary>The load exists but does not belong to the authenticated caller.</summary>
    LOAD_NOT_OWNED,

    /// <summary>
    /// A protected endpoint was called with no access token, or one that's missing, malformed,
    /// expired, or fails signature/issuer/audience validation. Written by the JwtBearer handler's
    /// <c>OnChallenge</c> event (<c>Program.cs</c>), not thrown as an <c>ApiException</c>.
    /// </summary>
    UNAUTHORIZED,

    /// <summary>
    /// A valid, authenticated caller was denied by a role/policy check (e.g. a future
    /// <c>[Authorize(Roles = ...)]</c> failure). Written by the JwtBearer handler's
    /// <c>OnForbidden</c> event (<c>Program.cs</c>), not thrown as an <c>ApiException</c>.
    /// </summary>
    FORBIDDEN,

    /// <summary>An unhandled exception was caught by the global exception-handling middleware.</summary>
    INTERNAL_SERVER_ERROR
}
