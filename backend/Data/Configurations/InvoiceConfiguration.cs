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

        builder.HasIndex(x => x.InvoiceNumber).IsUnique();

        builder.Property(x => x.Amount).HasPrecision(12, 2);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.Trip)
            .WithOne(t => t.Invoice)
            .HasForeignKey<Invoice>(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
