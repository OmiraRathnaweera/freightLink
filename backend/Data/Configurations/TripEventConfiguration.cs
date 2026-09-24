using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class TripEventConfiguration : IEntityTypeConfiguration<TripEvent>
{
    public void Configure(EntityTypeBuilder<TripEvent> builder)
    {
        builder.HasKey(x => x.TripEventId);
        builder.Property(x => x.TripEventId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.SnapshotLat).HasPrecision(9, 6);
        builder.Property(x => x.SnapshotLng).HasPrecision(9, 6);

        builder.HasOne(x => x.Trip)
            .WithMany(t => t.Events)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RecordedByUser)
            .WithMany()
            .HasForeignKey(x => x.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_tripevent_transition", "\"FromStatus\" IS DISTINCT FROM \"ToStatus\"");
            t.HasCheckConstraint("ck_tripevent_lat", "\"SnapshotLat\" IS NULL OR \"SnapshotLat\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("ck_tripevent_lng", "\"SnapshotLng\" IS NULL OR \"SnapshotLng\" BETWEEN -180 AND 180");
        });
    }
}
