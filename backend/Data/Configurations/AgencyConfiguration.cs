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
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_agencies (see
        // AddDatabaseConstraintsAndTriggers migration) overwrites UpdatedAt on every UPDATE.
        // Without this, EF has no way to know the trigger changed the column and would keep
        // the stale in-memory value after SaveChanges instead of reading back what Postgres
        // actually stored.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

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
