using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.HasKey(x => x.PaymentWebhookEventId);
        builder.Property(x => x.PaymentWebhookEventId).HasDefaultValueSql("gen_random_uuid()");

        // Deliberately no HasOne/FK: an unverified gateway callback must be recorded
        // before it can be trusted or linked to a Payment.

        // ProcessingStatus/ErrorMessage are intentionally mutable (Received -> SignatureRejected/
        // Duplicate/Applied/Error) — a dedicated trigger (fn_protect_webhook_receipt, see the
        // FixPaymentWebhookEventAppendOnlyTrigger migration) permits updates to only those two
        // columns while keeping the raw-receipt fields (GatewayRef, RawPayloadHash, SignatureValid,
        // ReceivedAt) and DELETE blocked. No Fluent API surface exists for column-selective
        // immutability, so this can't be expressed here.

        // Not unique: gateways redeliver identical callbacks (retries/at-least-once delivery),
        // and each delivery must still be recorded as its own row (first Applied/etc., repeats
        // Duplicate) — that's exactly what WebhookProcessingStatus.Duplicate exists to classify.
        // The real one-time-application guarantee lives on Payment.uq_payment_gatewayref instead;
        // this index only speeds up the app's own duplicate-hash lookup.
        builder.HasIndex(x => x.RawPayloadHash).HasDatabaseName("ix_pwe_payloadhash");

        builder.ToTable(t => t.HasCheckConstraint("ck_pwe_error",
            "\"ProcessingStatus\" <> 'Error' OR \"ErrorMessage\" IS NOT NULL"));
    }
}
