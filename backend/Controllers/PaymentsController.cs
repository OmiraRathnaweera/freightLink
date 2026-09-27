using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Payments;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Payment endpoints for PayHere hosted checkout initiation and asynchronous webhook / IPN callback handling.
/// </summary>
[ApiController]
public class PaymentsController : ControllerBase
{
    private const string CheckoutRoles = nameof(UserRole.Shipper) + "," + nameof(UserRole.Admin);

    private readonly IPayHereService _payHereService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPayHereService payHereService,
        ILogger<PaymentsController> logger)
    {
        _payHereService = payHereService;
        _logger = logger;
    }

    /// <summary>
    /// Initiates a PayHere checkout session for an unpaid invoice (Authenticated Shipper only).
    /// Returns the pre-signed checkout parameters and MD5 verification hash.
    /// </summary>
    /// <param name="id">The invoice ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with PayHere checkout fields and verification hash.</returns>
    [HttpPost("api/invoices/{id:guid}/payhere-checkout")]
    [HttpPost("api/v1/invoices/{id:guid}/payhere-checkout")]
    [Authorize(Roles = CheckoutRoles)]
    [ProducesResponseType(typeof(PayHereCheckoutResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PayHereCheckoutResponseDto>> InitiateCheckout(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _payHereService.CreateCheckoutSessionAsync(
            id,
            GetCurrentUserId(),
            GetCurrentUserRole(),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Asynchronous server-to-server IPN / webhook callback from PayHere.
    /// Sent as application/x-www-form-urlencoded after a payment attempt.
    /// Validates MD5 signature using merchant secret and transitions invoice to Paid upon status 2.
    /// </summary>
    /// <param name="callback">URL-encoded form data sent by PayHere.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK to acknowledge receipt.</returns>
    [HttpPost("api/payments/payhere/notify")]
    [HttpPost("api/v1/payments/payhere/notify")]
    [HttpPost("api/payments/notify")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded", "application/json", "multipart/form-data")]
    public async Task<IActionResult> ProcessPayHereWebhook(
        [FromForm] PayHereWebhookDto callback,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received PayHere IPN notification for Order: {OrderId}, Payment: {PaymentId}, Status: {StatusCode}",
            callback.OrderId, callback.PaymentId, callback.StatusCode);

        // Capture raw payload for audit hashing
        string rawPayload = string.Empty;
        if (Request.HasFormContentType)
        {
            var formPairs = Request.Form.Select(kv => $"{kv.Key}={kv.Value}");
            rawPayload = string.Join("&", formPairs);

            // Fallback bindings if model binding didn't catch specific fields
            callback.MerchantId ??= Request.Form["merchant_id"].FirstOrDefault();
            callback.OrderId ??= Request.Form["order_id"].FirstOrDefault();
            callback.PaymentId ??= Request.Form["payment_id"].FirstOrDefault();
            callback.PayhereAmount ??= Request.Form["payhere_amount"].FirstOrDefault();
            callback.PayhereCurrency ??= Request.Form["payhere_currency"].FirstOrDefault();
            callback.StatusCode ??= Request.Form["status_code"].FirstOrDefault();
            callback.Md5Sig ??= Request.Form["md5sig"].FirstOrDefault();
            callback.Method ??= Request.Form["method"].FirstOrDefault();
        }

        await _payHereService.HandleWebhookCallbackAsync(callback, rawPayload, cancellationToken);

        // PayHere expects HTTP 200 to acknowledge and stop retrying
        return Ok(new { status = "acknowledged" });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid user id.");
        }

        return userId;
    }

    private UserRole GetCurrentUserRole()
    {
        var roleClaim = User.FindFirstValue(ClaimTypes.Role);
        if (!Enum.TryParse<UserRole>(roleClaim, true, out var role))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid role.");
        }

        return role;
    }
}
