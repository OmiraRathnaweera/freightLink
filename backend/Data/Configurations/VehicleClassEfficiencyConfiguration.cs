using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

/// <summary>EF Core mapping for <see cref="VehicleClassEfficiency"/>.</summary>
public class VehicleClassEfficiencyConfiguration : IEntityTypeConfiguration<VehicleClassEfficiency>
{
    /// <summary>Configures keys, FKs, indexes, and check constraints for <see cref="VehicleClassEfficiency"/>.</summary>
    public void Configure(EntityTypeBuilder<VehicleClassEfficiency> builder)
    {
        builder.HasKey(x => x.VehicleClassEfficiencyId);
        builder.Property(x => x.VehicleClassEfficiencyId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_vehicleclassefficiencies overwrites
        // UpdatedAt on every UPDATE (the soft-delete itself) — needed so EF reads back the
        // trigger-written value instead of keeping the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.SetByUser)
            .WithMany()
            .HasForeignKey(x => x.SetByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DeletedByUser)
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Not unique — multiple historical rows per ClassLabel are expected. Supports the "current
        // figure for this class" lookup (latest EffectiveFrom, not soft-deleted).
        builder.HasIndex(x => new { x.ClassLabel, x.EffectiveFrom });

        builder.ToTable(t => t.HasCheckConstraint("ck_vce_consumption_positive", "\"FuelConsumptionLPer100Km\" > 0"));
        builder.ToTable(t => t.HasCheckConstraint("ck_vce_payload_bounds",
            "\"MinPayloadKg\" >= 0 AND (\"MaxPayloadKg\" IS NULL OR \"MaxPayloadKg\" > \"MinPayloadKg\")"));
    }
}
