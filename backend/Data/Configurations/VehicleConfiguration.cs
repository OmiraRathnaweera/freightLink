using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.HasKey(x => x.VehicleId);
        builder.Property(x => x.VehicleId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => new { x.AgencyId, x.RegistrationNo })
            .IsUnique()
            .HasDatabaseName("uq_vehicle_agency_regno");

        builder.Property(x => x.CapacityKg).HasPrecision(10, 2);
        builder.Property(x => x.VolumeM3).HasPrecision(10, 3);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_vehicles overwrites UpdatedAt on
        // every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.Agency)
            .WithMany(a => a.Vehicles)
            .HasForeignKey(x => x.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_vehicle_capacity", "\"CapacityKg\" > 0");
            t.HasCheckConstraint("ck_vehicle_volume", "\"VolumeM3\" > 0");
        });
    }
}
