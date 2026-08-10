using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.PaymentId);
        builder.Property(x => x.PaymentId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Amount).HasPrecision(12, 2);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.GatewayRef).IsUnique().HasDatabaseName("uq_payment_gatewayref");

        builder.HasIndex(x => new { x.InvoiceId, x.AttemptNo })
            .IsUnique()
            .HasDatabaseName("uq_payment_attempt");

        builder.HasIndex(x => x.InvoiceId)
            .IsUnique()
            .HasDatabaseName("ux_payment_success")
            .HasFilter("\"Status\" = 'Success'");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_payment_amount", "\"Amount\" > 0");
            t.HasCheckConstraint("ck_payment_attempt", "\"AttemptNo\" >= 1");
        });
    }
}
