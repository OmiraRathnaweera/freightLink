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

    /// <summary>The requested agency could not be found.</summary>
    AGENCY_NOT_FOUND,

    /// <summary>The requested load could not be found.</summary>
    LOAD_NOT_FOUND,

    /// <summary>A load's <c>PickupWindowEnd</c> was not after its <c>PickupWindowStart</c>.</summary>
    INVALID_PICKUP_WINDOW,

    /// <summary>An edit or cancel was attempted on a load whose current status doesn't allow it.</summary>
    INVALID_LOAD_STATUS_TRANSITION,

    /// <summary>A load cancellation was attempted without a reason.</summary>
    LOAD_CANCEL_REASON_REQUIRED,

    /// <summary>A load's pickup and dropoff coordinates were identical.</summary>
    LOAD_PICKUP_DROPOFF_IDENTICAL,

    /// <summary>A list query's <c>page</c>/<c>pageSize</c> combination requests a row offset beyond what EF/PostgreSQL can address.</summary>
    LOAD_PAGE_OUT_OF_RANGE,

    /// <summary>The server-generated load reference code collided with an existing one.</summary>
    LOAD_REFERENCE_CODE_CONFLICT,

    /// <summary>The load exists but does not belong to the authenticated caller.</summary>
    LOAD_NOT_OWNED,

    /// <summary>
    /// A concurrent write committed against this load between when it was loaded and when this
    /// request's <c>SaveChangesAsync</c> ran (caught via the <c>Load</c> entity's xmin concurrency
    /// token). The caller should re-fetch the load and retry.
    /// </summary>
    LOAD_CONCURRENCY_CONFLICT,
    /// <summary>A file upload/delete request was made with no file (or an empty file list) attached.</summary>
    FILE_REQUIRED,

    /// <summary>The uploaded file's extension is not an allowed image or PDF type.</summary>
    BLOCKED_FILE_TYPE,

    /// <summary>The uploaded file exceeds the maximum allowed size.</summary>
    FILE_TOO_LARGE,

    /// <summary>Cloudinary rejected or failed an upload (network/API failure, not a validation failure).</summary>
    FILE_UPLOAD_FAILED,

    /// <summary>Cloudinary rejected or failed a delete (network/API failure — a merely-missing publicId is not an error; see <see cref="FreightLink.Api.DTOs.Files.FileDeleteResultDto"/>).</summary>
    FILE_DELETE_FAILED,

    /// <summary>A delete was attempted on a file the authenticated caller did not upload.</summary>
    FILE_NOT_OWNED,

    /// <summary>A delete was attempted on a file still attached to a Load via <c>LoadFile</c>.</summary>
    FILE_IN_USE,

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
