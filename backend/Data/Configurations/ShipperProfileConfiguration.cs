using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class ShipperProfileConfiguration : IEntityTypeConfiguration<ShipperProfile>
{
    public void Configure(EntityTypeBuilder<ShipperProfile> builder)
    {
        builder.HasKey(x => x.UserId);

        builder.HasIndex(x => x.BusinessRegNo).IsUnique().HasDatabaseName("uq_shipperprofile_regno");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_shipperprofiles overwrites UpdatedAt
        // on every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.User)
            .WithOne(u => u.ShipperProfile)
            .HasForeignKey<ShipperProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
