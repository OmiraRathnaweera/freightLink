using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(x => x.InvoiceId);
        builder.Property(x => x.InvoiceId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => x.InvoiceNumber).IsUnique().HasDatabaseName("uq_invoice_number");

        // Nullable TripId with partial unique index so manual invoices without trips can coexist
        builder.HasIndex(x => x.TripId)
            .IsUnique()
            .HasFilter("\"TripId\" IS NOT NULL")
            .HasDatabaseName("uq_invoice_trip_id");

        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.Subtotal).HasPrecision(12, 2).HasDefaultValue(0m);
        builder.Property(x => x.TaxTotal).HasPrecision(12, 2).HasDefaultValue(0m);
        builder.Property(x => x.DiscountTotal).HasPrecision(12, 2).HasDefaultValue(0m);

        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.VoidReason).HasMaxLength(1000);
        builder.Property(x => x.PaymentReference).HasMaxLength(128);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.Trip)
            .WithOne(t => t.Invoice)
            .HasForeignKey<Invoice>(x => x.TripId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Recipient)
            .WithMany()
            .HasForeignKey(x => x.RecipientId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VoidedByUser)
            .WithMany()
            .HasForeignKey(x => x.VoidedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaymentProofFile)
            .WithMany()
            .HasForeignKey(x => x.PaymentProofFileId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PaymentProofUploadedByUser)
            .WithMany()
            .HasForeignKey(x => x.PaymentProofUploadedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.LineItems)
            .WithOne(li => li.Invoice)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_invoice_amount", "\"Amount\" >= 0");
            t.HasCheckConstraint("ck_invoice_currency", "\"Currency\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("ck_invoice_due",
                "\"DueDate\" IS NULL OR \"IssuedAt\" IS NULL OR \"DueDate\" >= (\"IssuedAt\" AT TIME ZONE 'UTC')::date");
        });
    }
}
