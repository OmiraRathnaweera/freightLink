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

        // Explicit name lets InvoiceService.CreateAsync identify this constraint by name
        // in the PostgresException.ConstraintName when a race between two concurrent
        // create-for-the-same-trip requests hits the database. Without a stable name the
        // catch clause cannot distinguish a TripId collision from an InvoiceNumber collision.
        builder.HasIndex(x => x.TripId).IsUnique().HasDatabaseName("uq_invoice_trip_id");

        builder.Property(x => x.Amount).HasPrecision(12, 2);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_invoices overwrites UpdatedAt on
        // every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.Trip)
            .WithOne(t => t.Invoice)
            .HasForeignKey<Invoice>(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_invoice_amount", "\"Amount\" > 0");
            t.HasCheckConstraint("ck_invoice_currency", "\"Currency\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("ck_invoice_due",
                "\"DueDate\" IS NULL OR \"IssuedAt\" IS NULL OR \"DueDate\" >= (\"IssuedAt\" AT TIME ZONE 'UTC')::date");
        });
    }
}
