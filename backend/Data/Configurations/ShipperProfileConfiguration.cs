using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class ShipperProfileConfiguration : IEntityTypeConfiguration<ShipperProfile>
{
    public void Configure(EntityTypeBuilder<ShipperProfile> builder)
    {
        builder.HasKey(x => x.UserId);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.User)
            .WithOne(u => u.ShipperProfile)
            .HasForeignKey<ShipperProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
