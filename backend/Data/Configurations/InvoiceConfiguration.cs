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

        builder.Property(x => x.Amount).HasPrecision(12, 2);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.Trip)
            .WithOne(t => t.Invoice)
            .HasForeignKey<Invoice>(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_invoice_amount", "\"Amount\" > 0");
            t.HasCheckConstraint("ck_invoice_currency", "\"Currency\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("ck_invoice_due",
                "\"DueDate\" IS NULL OR \"DueDate\" >= (\"IssuedAt\" AT TIME ZONE 'UTC')::date");
        });
    }
}
