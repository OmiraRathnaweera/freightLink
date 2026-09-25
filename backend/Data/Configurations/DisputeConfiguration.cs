using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.HasKey(x => x.DisputeId);
        builder.Property(x => x.DisputeId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_disputes overwrites UpdatedAt on
        // every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.Trip)
            .WithMany(t => t.Disputes)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RaisedByUser)
            .WithMany()
            .HasForeignKey(x => x.RaisedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TripId, x.Category })
            .IsUnique()
            .HasDatabaseName("ux_dispute_open")
            .HasFilter("\"Status\" IN ('Open','UnderReview','Raised')");

        builder.ToTable(t => t.HasCheckConstraint("ck_dispute_description",
            "length(trim(\"Description\")) >= 10"));
    }
}
