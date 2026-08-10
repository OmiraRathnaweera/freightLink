using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class TripEvidenceConfiguration : IEntityTypeConfiguration<TripEvidence>
{
    public void Configure(EntityTypeBuilder<TripEvidence> builder)
    {
        builder.HasKey(x => x.TripEvidenceId);
        builder.Property(x => x.TripEvidenceId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CapturedLat).HasPrecision(9, 6);
        builder.Property(x => x.CapturedLng).HasPrecision(9, 6);

        builder.HasOne(x => x.Trip)
            .WithMany(t => t.Evidence)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CapturedByUser)
            .WithMany()
            .HasForeignKey(x => x.CapturedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StorageKey).IsUnique().HasDatabaseName("uq_tripevidence_storagekey");

        builder.HasIndex(x => new { x.TripId, x.EvidenceType })
            .IsUnique()
            .HasDatabaseName("uq_tripevidence_type");
    }
}
