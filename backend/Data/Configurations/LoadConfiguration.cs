using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class LoadConfiguration : IEntityTypeConfiguration<Load>
{
    public void Configure(EntityTypeBuilder<Load> builder)
    {
        builder.HasKey(x => x.LoadId);
        builder.Property(x => x.LoadId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => x.ReferenceCode).IsUnique().HasDatabaseName("uq_load_reference");

        builder.Property(x => x.WeightKg).HasPrecision(10, 2);
        builder.Property(x => x.VolumeM3).HasPrecision(10, 3);
        builder.Property(x => x.PickupLat).HasPrecision(9, 6);
        builder.Property(x => x.PickupLng).HasPrecision(9, 6);
        builder.Property(x => x.DropoffLat).HasPrecision(9, 6);
        builder.Property(x => x.DropoffLng).HasPrecision(9, 6);
        builder.Property(x => x.EstimatedPrice).HasPrecision(12, 2);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_loads overwrites UpdatedAt on
        // every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.ShipperUser)
            .WithMany()
            .HasForeignKey(x => x.ShipperUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("ix_load_posted")
            .HasFilter("\"Status\" = 'Posted'");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_load_weight", "\"WeightKg\" > 0");
            t.HasCheckConstraint("ck_load_volume", "\"VolumeM3\" > 0");
            t.HasCheckConstraint("ck_load_price", "\"EstimatedPrice\" IS NULL OR \"EstimatedPrice\" > 0");
            t.HasCheckConstraint("ck_load_pickup_lat", "\"PickupLat\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("ck_load_pickup_lng", "\"PickupLng\" BETWEEN -180 AND 180");
            t.HasCheckConstraint("ck_load_dropoff_lat", "\"DropoffLat\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("ck_load_dropoff_lng", "\"DropoffLng\" BETWEEN -180 AND 180");
            t.HasCheckConstraint("ck_load_window", "\"PickupWindowEnd\" > \"PickupWindowStart\"");
            t.HasCheckConstraint("ck_load_distinct_points",
                "\"PickupLat\" <> \"DropoffLat\" OR \"PickupLng\" <> \"DropoffLng\"");
        });
    }
}
