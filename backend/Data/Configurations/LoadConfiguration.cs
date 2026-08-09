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

        builder.HasIndex(x => x.ReferenceCode).IsUnique();

        builder.Property(x => x.WeightKg).HasPrecision(10, 2);
        builder.Property(x => x.VolumeM3).HasPrecision(10, 3);
        builder.Property(x => x.PickupLat).HasPrecision(9, 6);
        builder.Property(x => x.PickupLng).HasPrecision(9, 6);
        builder.Property(x => x.DropoffLat).HasPrecision(9, 6);
        builder.Property(x => x.DropoffLng).HasPrecision(9, 6);
        builder.Property(x => x.EstimatedPrice).HasPrecision(12, 2);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.ShipperUser)
            .WithMany()
            .HasForeignKey(x => x.ShipperUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
