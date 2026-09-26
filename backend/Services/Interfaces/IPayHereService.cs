using FreightLink.Api.DTOs.Payments;
using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Service contract for PayHere payment gateway checkout initiation and webhook processing.
/// </summary>
public interface IPayHereService
{
    /// <summary>
    /// Generates pre-signed PayHere checkout parameters for an unpaid invoice.
    /// </summary>
    /// <param name="invoiceId">The invoice ID to generate payment checkout for.</param>
    /// <param name="userId">The authenticated user ID.</param>
    /// <param name="role">The authenticated user role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Populated checkout response DTO with MD5 hash and form fields.</returns>
    Task<PayHereCheckoutResponseDto> CreateCheckoutSessionAsync(
        Guid invoiceId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken);

    /// <summary>
    /// Processes an asynchronous server-to-server IPN / webhook callback from PayHere.
    /// Verifies MD5 signature and updates invoice and payment records idempotently.
    /// </summary>
    /// <param name="callback">The webhook parameters sent by PayHere.</param>
    /// <param name="rawPayload">Raw request payload string for hash audit logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully processed or acknowledged.</returns>
    Task<bool> HandleWebhookCallbackAsync(
        PayHereWebhookDto callback,
        string? rawPayload,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calculates the PayHere checkout MD5 verification hash:
    /// strtoupper(md5(merchant_id + order_id + formatted_amount + currency + strtoupper(md5(merchant_secret))))
    /// </summary>
    string GenerateCheckoutHash(
        string merchantId,
        string orderId,
        string formattedAmount,
        string currency,
        string merchantSecret);

    /// <summary>
    /// Calculates the expected PayHere webhook MD5 signature:
    /// strtoupper(md5(merchant_id + order_id + payhere_amount + payhere_currency + status_code + strtoupper(md5(merchant_secret))))
    /// </summary>
    string GenerateWebhookSignature(
        string merchantId,
        string orderId,
        string payhereAmount,
        string payhereCurrency,
        string statusCode,
        string merchantSecret);
}
