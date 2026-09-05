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

    /// <summary>The agency exists but does not belong to the authenticated caller.</summary>
    AGENCY_NOT_OWNED,

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

    /// <summary>The requested trip could not be found.</summary>
    TRIP_NOT_FOUND,

    /// <summary>The requested invoice could not be found.</summary>
    INVOICE_NOT_FOUND,

    /// <summary>The invoice exists but does not belong to the caller's organization/role.</summary>
    INVOICE_NOT_OWNED,

    /// <summary>An invoice already exists for this trip (1-to-1 relationship enforced).</summary>
    INVOICE_ALREADY_EXISTS_FOR_TRIP,

    /// <summary>An invoice transition or update was attempted that violates allowed lifecycle rules.</summary>
    INVALID_INVOICE_STATUS_TRANSITION,

    /// <summary>An invalid invoice amount was supplied.</summary>
    INVALID_INVOICE_AMOUNT,

    /// <summary>The requested dispute could not be found.</summary>
    DISPUTE_NOT_FOUND,

    /// <summary>The dispute exists but does not belong to the authenticated caller.</summary>
    DISPUTE_NOT_OWNED,

    /// <summary>A dispute mutation or resolution was attempted that violates allowed lifecycle rules.</summary>
    INVALID_DISPUTE_STATUS_TRANSITION,

    /// <summary>The dispute has already been resolved or rejected.</summary>
    DISPUTE_ALREADY_RESOLVED,
    /// <summary>An attach was attempted referencing an UploadedFile publicId that does not exist.</summary>
    LOAD_FILE_UPLOAD_NOT_FOUND,

    /// <summary>A detach was attempted on a LoadFile attachment that does not exist under the given load.</summary>
    LOAD_FILE_NOT_FOUND,

    /// <summary>No <c>FuelPriceRate</c> exists with the requested id.</summary>
    FUEL_PRICE_RATE_NOT_FOUND,

    /// <summary>A soft delete was attempted on a <c>FuelPriceRate</c> row that is already soft-deleted.</summary>
    FUEL_PRICE_RATE_ALREADY_DELETED,

    /// <summary>No <c>VehicleClassEfficiency</c> exists with the requested id.</summary>
    VEHICLE_CLASS_EFFICIENCY_NOT_FOUND,

    /// <summary>A soft delete was attempted on a <c>VehicleClassEfficiency</c> row that is already soft-deleted.</summary>
    VEHICLE_CLASS_EFFICIENCY_ALREADY_DELETED,

    /// <summary>A <c>VehicleClassEfficiency</c> row's <c>MaxPayloadKg</c> was not strictly greater than its <c>MinPayloadKg</c> (mirrors <c>ck_vce_payload_bounds</c>).</summary>
    VEHICLE_CLASS_EFFICIENCY_INVALID_PAYLOAD_BAND,

    /// <summary>A new <c>VehicleClassEfficiency</c> row's payload band overlaps another class's current band.</summary>
    VEHICLE_CLASS_EFFICIENCY_BAND_OVERLAP,

    /// <summary>A new <c>VehicleClassEfficiency</c> row's payload or volume band would leave a gap in that dimension's coverage.</summary>
    VEHICLE_CLASS_EFFICIENCY_BAND_GAP,

    /// <summary>A <c>VehicleClassEfficiency</c> row's <c>MaxVolumeM3</c> was not strictly greater than its <c>MinVolumeM3</c> (mirrors <c>ck_vce_volume_bounds</c>).</summary>
    VEHICLE_CLASS_EFFICIENCY_INVALID_VOLUME_BAND,

    /// <summary>
    /// No current <c>FuelPriceRate</c> exists for the requested fuel type, no <c>VehicleClassEfficiency</c>
    /// tier covers the requested weight/volume, or no current <c>PricingFormulaConfig</c> exists.
    /// Thrown by <c>IPricingConfigService</c>'s current-value lookups; not raised by <c>LoadService</c>,
    /// which does not depend on any pricing config existing.
    /// </summary>
    PRICING_CONFIG_MISSING,

    /// <summary>No <c>PricingFormulaConfig</c> exists with the requested id.</summary>
    PRICING_FORMULA_CONFIG_NOT_FOUND,

    /// <summary>A soft delete was attempted on a <c>PricingFormulaConfig</c> row that is already soft-deleted.</summary>
    PRICING_FORMULA_CONFIG_ALREADY_DELETED,

    /// <summary>
    /// <c>POST /internal/pricing/estimate</c> was called with a missing or incorrect
    /// <c>X-Internal-Api-Key</c> header. Thrown by <c>InternalApiKeyAuthFilter</c> — distinct from
    /// <see cref="UNAUTHORIZED"/>, which is reserved for JWT failures written directly by the
    /// JwtBearer handler, not thrown as an <c>ApiException</c>.
    /// </summary>
    INTERNAL_API_KEY_INVALID,

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
    INTERNAL_SERVER_ERROR,

    /// <summary>The SMTP send failed (network/auth/provider failure, not a validation failure) — thrown by <c>GmailEmailService</c>.</summary>
    EMAIL_SEND_FAILED,

    /// <summary>
    /// <c>POST /internal/agent-workflow-runs</c> was called with a <c>LoadId</c> that doesn't exist.
    /// Thrown by <c>AgentWorkflowService.CreateAsync</c>.
    /// </summary>
    LOAD_NOT_FOUND_FOR_WORKFLOW_RUN,

    /// <summary>
    /// <c>POST /internal/agent-workflow-runs</c> was called with a <c>TriggeredByUserId</c> that
    /// doesn't exist. Thrown by <c>AgentWorkflowService.CreateAsync</c>.
    /// </summary>
    USER_NOT_FOUND_FOR_WORKFLOW_RUN,

    /// <summary>
    /// <c>POST /internal/agent-workflow-runs</c> was called with a <c>(LoadId, AttemptNo)</c> pair
    /// that already has a run (mirrors <c>uq_awr_load_attempt</c>). Each attempt number for a given
    /// load must be started at most once.
    /// </summary>
    WORKFLOW_RUN_DUPLICATE_ATTEMPT,

    /// <summary>
    /// <c>POST /internal/agent-workflow-runs/{workflowRunId}/steps</c> was called with a
    /// <c>workflowRunId</c> that doesn't exist. Thrown by <c>AgentWorkflowService.ReportStepAsync</c>.
    /// </summary>
    WORKFLOW_RUN_NOT_FOUND,

    /// <summary>
    /// <c>POST /internal/agent-workflow-runs/{workflowRunId}/steps</c> was called twice for the same
    /// <c>(WorkflowRunId, StepNo)</c> pair (mirrors <c>uq_agentstep_order</c>). Each step number for a
    /// given run may be reported at most once.
    /// </summary>
    AGENT_STEP_DUPLICATE
}

