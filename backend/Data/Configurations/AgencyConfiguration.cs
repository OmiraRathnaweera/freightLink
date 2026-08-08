using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> builder)
    {
        builder.HasKey(x => x.AgencyId);
        builder.Property(x => x.AgencyId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => x.BusinessRegNo).IsUnique();

        builder.Property(x => x.YardLat).HasPrecision(9, 6);
        builder.Property(x => x.YardLng).HasPrecision(9, 6);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
    }
}
