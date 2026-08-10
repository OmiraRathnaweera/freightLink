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

        builder.HasIndex(x => x.RawPayloadHash).IsUnique().HasDatabaseName("uq_pwe_payloadhash");

        builder.ToTable(t => t.HasCheckConstraint("ck_pwe_error",
            "\"ProcessingStatus\" <> 'Error' OR \"ErrorMessage\" IS NOT NULL"));
    }
}
