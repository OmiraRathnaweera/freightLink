using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

/// <summary>
/// Raw record of a single gateway callback delivery, logged before it is trusted or linked to a
/// <see cref="Payment"/> (deliberately no FK — see PaymentWebhookEventConfiguration). Gateways
/// redeliver identical callbacks, so <see cref="RawPayloadHash"/> is intentionally not unique
/// (ix_pwe_payloadhash is a plain index); the real one-time-application guarantee lives on
/// <see cref="Payment.GatewayRef"/> (uq_payment_gatewayref) instead.
/// Partially, not fully, append-only at the database level: <c>trg_protect_webhook_receipt</c>
/// (added in the FixPaymentWebhookEventAppendOnlyTrigger migration) blocks DELETE and blocks
/// UPDATE of every column except <see cref="ProcessingStatus"/>/<see cref="ErrorMessage"/> — those
/// two are intentionally mutable so a delivery can advance from Received to
/// SignatureRejected/Duplicate/Applied/Error. Any other UPDATE attempt, or any DELETE, raises a raw
/// <c>PostgresException</c> that service code should catch/translate into a domain error rather
/// than let it surface as an unhandled 500 once a service layer exists.
/// </summary>
public class PaymentWebhookEvent
{
    /// <summary>Primary key. Immutable once inserted.</summary>
    public Guid PaymentWebhookEventId { get; set; }

    /// <summary>Gateway's reference for the underlying payment. Immutable once inserted.</summary>
    public string GatewayRef { get; set; } = string.Empty;

    /// <summary>Hash of the raw callback payload, used for the app's own duplicate-delivery
    /// detection. Not unique — see class summary. Immutable once inserted.</summary>
    public string RawPayloadHash { get; set; } = string.Empty;

    /// <summary>Whether the gateway signature on this callback verified. Immutable once inserted.</summary>
    public bool SignatureValid { get; set; }

    /// <summary>Current processing outcome. Mutable — advances after insert as the callback is
    /// evaluated.</summary>
    public WebhookProcessingStatus ProcessingStatus { get; set; }

    /// <summary>Required when <see cref="ProcessingStatus"/> is Error (enforced by ck_pwe_error).
    /// Mutable alongside <see cref="ProcessingStatus"/>.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Timestamp the callback was received. Immutable once inserted.</summary>
    public DateTimeOffset ReceivedAt { get; set; }
}
