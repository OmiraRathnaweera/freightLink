using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasKey(x => x.TripId);
        builder.Property(x => x.TripId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_trips overwrites UpdatedAt on
        // every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.Assignment)
            .WithOne(a => a.Trip)
            .HasForeignKey<Trip>(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vehicle)
            .WithMany(v => v.Trips)
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Driver)
            .WithMany(d => d.Trips)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.VehicleId)
            .IsUnique()
            .HasDatabaseName("ux_trip_vehicle_live")
            .HasFilter("\"Status\" IN ('Assigned','PickedUp','InTransit')");

        builder.HasIndex(x => x.DriverId)
            .IsUnique()
            .HasDatabaseName("ux_trip_driver_live")
            .HasFilter("\"Status\" IN ('Assigned','PickedUp','InTransit')");
    }
}
