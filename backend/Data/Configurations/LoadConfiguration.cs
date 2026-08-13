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

        // Optimistic concurrency via Postgres's built-in xmin system column, same pattern as
        // RefreshTokenConfiguration: guarantees that if two concurrent requests both load the same
        // Load (e.g. a racing Update and Cancel), only the first to SaveChanges commits — the second's
        // UPDATE affects zero rows under the hood and EF raises DbUpdateConcurrencyException
        // (translated to a 409 conflict by LoadService.UpdateAsync/CancelAsync) instead of both
        // silently succeeding and one clobbering the other.
        // NOTE: EF Core's provider-agnostic IsRowVersion() shadow-property pattern doesn't work here
        // — Npgsql/EF treats a plain `.Property<uint>("xmin").IsRowVersion()` as a brand-new column to
        // create via migration, which collides with Postgres's actual reserved system column of the
        // same name. UseXminAsConcurrencyToken() is obsolete in this package version but is still the
        // only API that correctly maps to the existing system column (read-only, no DDL) rather than
        // creating a new one — kept deliberately.
#pragma warning disable CS0618 // UseXminAsConcurrencyToken is obsolete; see note above for why the suggested replacement doesn't work for Postgres's real xmin column.
        builder.UseXminAsConcurrencyToken();
#pragma warning restore CS0618

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

        // GIN trigram indexes on lower(CargoDescription/ReferenceCode/PickupAddress/DropoffAddress)
        // — the four columns LoadService.GetListAsync's Search filter matches against with
        // .ToLower().Contains(term) — are created via raw SQL in the AddLoadSearchTrigramIndexes
        // migration, not here: EF's fluent index API has no way to express an index over an
        // expression like lower(column) rather than a plain mapped property, only a real Postgres
        // GIN/gin_trgm_ops index (via the pg_trgm extension, enabled in AppDbContext.OnModelCreating)
        // can accelerate that kind of leading-wildcard substring match at all — a plain B-tree
        // index, expression-based or not, cannot.

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
