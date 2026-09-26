using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Payments;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreightLink.Api.Services;

/// <summary>
/// Implements PayHere payment gateway integration (Sandbox/Test Mode & Production).
/// Handles checkout parameter generation, MD5 hash generation, and IPN webhook processing with signature verification.
/// </summary>
public class PayHereService : IPayHereService
{
    private readonly AppDbContext _dbContext;
    private readonly PayHereOptions _options;
    private readonly ILogger<PayHereService> _logger;

    public PayHereService(
        AppDbContext dbContext,
        IOptions<PayHereOptions> options,
        ILogger<PayHereService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PayHereCheckoutResponseDto> CreateCheckoutSessionAsync(
        Guid invoiceId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .Include(i => i.Trip)
                .ThenInclude(t => t.Assignment)
                    .ThenInclude(a => a.Load)
                        .ThenInclude(l => l.ShipperUser)
                            .ThenInclude(u => u.ShipperProfile)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

        if (invoice == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice with ID '{invoiceId}' was not found.");
        }

        // Verify caller permissions: Only the invoice's Shipper (or Admin) can initiate checkout
        var load = invoice.Trip?.Assignment?.Load;
        if (role == UserRole.Shipper && load != null && load.ShipperUserId != userId)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.INVOICE_NOT_OWNED, "You are not authorized to pay for an invoice you do not own.");
        }

        // Verify that the invoice is unpaid
        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVOICE_ALREADY_PAID, $"Invoice #{invoice.InvoiceNumber} is already settled and marked as Paid.");
        }

        if (invoice.Status == InvoiceStatus.Void)
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.INVALID_INVOICE_STATUS_TRANSITION, $"Invoice #{invoice.InvoiceNumber} is voided and cannot be paid.");
        }

        // Advance invoice from Draft/Issued to PaymentPending upon checkout initiation
        if (invoice.Status == InvoiceStatus.Draft || invoice.Status == InvoiceStatus.Issued)
        {
            invoice.Status = InvoiceStatus.PaymentPending;
            invoice.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Format amount strictly to 2 decimal places (e.g. "1500.00")
        string formattedAmount = invoice.Amount.ToString("F2", CultureInfo.InvariantCulture);
        string orderId = invoice.InvoiceId.ToString();
        string currency = string.IsNullOrWhiteSpace(invoice.Currency) ? "LKR" : invoice.Currency.Trim().ToUpperInvariant();
        string merchantId = _options.MerchantId;
        string merchantSecret = _options.MerchantSecret;

        // Calculate checkout verification hash
        string hash = GenerateCheckoutHash(merchantId, orderId, formattedAmount, currency, merchantSecret);

        // Resolve customer details from ShipperUser
        var shipperUser = load?.ShipperUser;
        string fullName = shipperUser?.FullName ?? "FreightLink Shipper";
        string[] nameParts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string firstName = nameParts.Length > 0 ? nameParts[0] : "Customer";
        string lastName = nameParts.Length > 1 ? nameParts[1] : "Shipper";

        string email = !string.IsNullOrWhiteSpace(shipperUser?.Email) ? shipperUser.Email : "shipper@freightlink.lk";
        string phone = !string.IsNullOrWhiteSpace(shipperUser?.PhoneE164) ? shipperUser.PhoneE164 : "+94770000000";
        string address = !string.IsNullOrWhiteSpace(shipperUser?.ShipperProfile?.BillingAddress)
            ? shipperUser.ShipperProfile.BillingAddress
            : (!string.IsNullOrWhiteSpace(load?.PickupAddress) ? load.PickupAddress : "Colombo Logistics Hub");
        string city = "Colombo";
        string country = "Sri Lanka";

        // Build return and cancel URLs by substituting invoice id
        string returnUrl = (_options.ReturnUrl ?? string.Empty)
            .Replace(":id", orderId)
            .Replace("{id}", orderId);

        string cancelUrl = (_options.CancelUrl ?? string.Empty)
            .Replace(":id", orderId)
            .Replace("{id}", orderId);

        return new PayHereCheckoutResponseDto
        {
            ActionUrl = _options.GetEffectiveCheckoutUrl(),
            MerchantId = merchantId,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl,
            NotifyUrl = _options.NotifyUrl,
            OrderId = orderId,
            Items = $"FreightLink Invoice #{invoice.InvoiceNumber}",
            Currency = currency,
            Amount = formattedAmount,
            Hash = hash,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = phone,
            Address = address,
            City = city,
            Country = country,
            InvoiceId = invoice.InvoiceId,
            InvoiceNumber = invoice.InvoiceNumber
        };
    }

    /// <inheritdoc />
    public async Task<bool> HandleWebhookCallbackAsync(
        PayHereWebhookDto callback,
        string? rawPayload,
        CancellationToken cancellationToken)
    {
        string merchantId = callback.MerchantId ?? string.Empty;
        string orderId = callback.OrderId ?? string.Empty;
        string payhereAmount = callback.PayhereAmount ?? string.Empty;
        string payhereCurrency = callback.PayhereCurrency ?? string.Empty;
        string statusCode = callback.StatusCode ?? string.Empty;
        string incomingSig = callback.Md5Sig ?? string.Empty;
        string paymentId = callback.PaymentId ?? string.Empty;

        // Verify incoming signature against computed local signature
        string localSig = GenerateWebhookSignature(
            merchantId,
            orderId,
            payhereAmount,
            payhereCurrency,
            statusCode,
            _options.MerchantSecret);

        bool isSignatureValid = string.Equals(localSig, incomingSig, StringComparison.OrdinalIgnoreCase);

        // Record incoming callback in PaymentWebhookEvents table
        string payloadHash = ComputePayloadHash(rawPayload ?? $"{merchantId}:{orderId}:{paymentId}:{statusCode}:{incomingSig}");
        var webhookEvent = new PaymentWebhookEvent
        {
            PaymentWebhookEventId = Guid.NewGuid(),
            GatewayRef = string.IsNullOrWhiteSpace(paymentId) ? Guid.NewGuid().ToString() : paymentId,
            RawPayloadHash = payloadHash,
            SignatureValid = isSignatureValid,
            ProcessingStatus = isSignatureValid ? WebhookProcessingStatus.Received : WebhookProcessingStatus.SignatureRejected,
            ErrorMessage = isSignatureValid ? null : "MD5 signature verification failed.",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _dbContext.PaymentWebhookEvents.Add(webhookEvent);

        if (!isSignatureValid)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "PayHere webhook MD5 signature mismatch for Order '{OrderId}'. Expected: '{Expected}', Received: '{Received}'",
                orderId, localSig, incomingSig);

            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.PAYMENT_SIGNATURE_INVALID, "Payment signature verification failed.");
        }

        // Locate invoice by OrderId (either GUID or InvoiceNumber)
        Invoice? invoice = null;
        if (Guid.TryParse(orderId, out var parsedInvoiceId))
        {
            invoice = await _dbContext.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceId == parsedInvoiceId, cancellationToken);
        }

        if (invoice == null)
        {
            invoice = await _dbContext.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceNumber == orderId, cancellationToken);
        }

        if (invoice == null)
        {
            webhookEvent.ProcessingStatus = WebhookProcessingStatus.Error;
            webhookEvent.ErrorMessage = $"Invoice associated with OrderId '{orderId}' was not found in the database.";
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError("PayHere webhook references unknown invoice for OrderId '{OrderId}'", orderId);
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.INVOICE_NOT_FOUND, $"Invoice with Order ID '{orderId}' not found.");
        }

        // Process status codes:
        // 2: Success
        // 0: Pending
        // -1: Canceled
        // -2: Failed
        // -3: Chargedback
        if (statusCode == "2")
        {
            // Idempotent handling: Check if already paid and applied
            bool alreadyApplied = invoice.Status == InvoiceStatus.Paid ||
                                  invoice.Payments.Any(p => p.GatewayRef == paymentId && p.Status == PaymentStatus.Success);

            if (alreadyApplied)
            {
                webhookEvent.ProcessingStatus = WebhookProcessingStatus.Duplicate;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Duplicate PayHere success notification for Invoice #{InvoiceNumber}, Payment #{PaymentId}. Acknowledged idempotently.",
                    invoice.InvoiceNumber, paymentId);
                return true;
            }

            // Parse payment amount
            if (!decimal.TryParse(payhereAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var paidAmount) || paidAmount <= 0)
            {
                paidAmount = invoice.Amount;
            }

            // Update invoice status to Paid
            invoice.Status = InvoiceStatus.Paid;
            invoice.UpdatedAt = DateTimeOffset.UtcNow;

            // Record Payment record
            int nextAttempt = (invoice.Payments.Any() ? invoice.Payments.Max(p => p.AttemptNo) : 0) + 1;
            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                InvoiceId = invoice.InvoiceId,
                GatewayRef = paymentId,
                Amount = paidAmount,
                Status = PaymentStatus.Success,
                AttemptNo = nextAttempt,
                ProcessedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Payments.Add(payment);
            webhookEvent.ProcessingStatus = WebhookProcessingStatus.Applied;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully processed PayHere payment for Invoice #{InvoiceNumber}. Status changed to Paid. PaymentRef: {PaymentRef}",
                invoice.InvoiceNumber, paymentId);
            return true;
        }

        if (statusCode == "0")
        {
            // Pending
            invoice.Status = InvoiceStatus.PaymentPending;
            invoice.UpdatedAt = DateTimeOffset.UtcNow;

            int nextAttempt = (invoice.Payments.Any() ? invoice.Payments.Max(p => p.AttemptNo) : 0) + 1;
            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                InvoiceId = invoice.InvoiceId,
                GatewayRef = string.IsNullOrWhiteSpace(paymentId) ? null : paymentId,
                Amount = invoice.Amount,
                Status = PaymentStatus.Pending,
                AttemptNo = nextAttempt,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Payments.Add(payment);
            webhookEvent.ProcessingStatus = WebhookProcessingStatus.Applied;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (statusCode == "-1" || statusCode == "-2" || statusCode == "-3")
        {
            // Canceled or Failed
            var paymentStatus = statusCode == "-1" ? PaymentStatus.Cancelled : PaymentStatus.Failed;
            if (statusCode == "-2")
            {
                invoice.Status = InvoiceStatus.Failed;
                invoice.UpdatedAt = DateTimeOffset.UtcNow;
            }

            int nextAttempt = (invoice.Payments.Any() ? invoice.Payments.Max(p => p.AttemptNo) : 0) + 1;
            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                InvoiceId = invoice.InvoiceId,
                GatewayRef = string.IsNullOrWhiteSpace(paymentId) ? null : paymentId,
                Amount = invoice.Amount,
                Status = paymentStatus,
                AttemptNo = nextAttempt,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Payments.Add(payment);
            webhookEvent.ProcessingStatus = WebhookProcessingStatus.Applied;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        // Unknown status code - log and acknowledge
        webhookEvent.ProcessingStatus = WebhookProcessingStatus.Applied;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public string GenerateCheckoutHash(
        string merchantId,
        string orderId,
        string formattedAmount,
        string currency,
        string merchantSecret)
    {
        string hashedSecret = ComputeMd5(merchantSecret);
        string payload = merchantId + orderId + formattedAmount + currency + hashedSecret;
        return ComputeMd5(payload);
    }

    /// <inheritdoc />
    public string GenerateWebhookSignature(
        string merchantId,
        string orderId,
        string payhereAmount,
        string payhereCurrency,
        string statusCode,
        string merchantSecret)
    {
        string hashedSecret = ComputeMd5(merchantSecret);
        string payload = merchantId + orderId + payhereAmount + payhereCurrency + statusCode + hashedSecret;
        return ComputeMd5(payload);
    }

    private static string ComputeMd5(string input)
    {
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }

    private static string ComputePayloadHash(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }
}
