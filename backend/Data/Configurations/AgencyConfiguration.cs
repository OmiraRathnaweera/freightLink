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

        builder.HasIndex(x => x.BusinessRegNo).IsUnique().HasDatabaseName("uq_agency_regno");

        builder.Property(x => x.YardLat).HasPrecision(9, 6);
        builder.Property(x => x.YardLng).HasPrecision(9, 6);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.YardLat, x.YardLng })
            .HasDatabaseName("ix_agency_active_yard")
            .HasFilter("\"Status\" = 'Active'");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_agency_yard_lat", "\"YardLat\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("ck_agency_yard_lng", "\"YardLng\" BETWEEN -180 AND 180");
        });
    }
}
