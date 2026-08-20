using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

/// <summary>EF Core mapping for <see cref="PricingFormulaConfig"/>.</summary>
public class PricingFormulaConfigConfiguration : IEntityTypeConfiguration<PricingFormulaConfig>
{
    /// <summary>Configures keys, FKs, indexes, and check constraints for <see cref="PricingFormulaConfig"/>.</summary>
    public void Configure(EntityTypeBuilder<PricingFormulaConfig> builder)
    {
        builder.HasKey(x => x.PricingFormulaConfigId);
        builder.Property(x => x.PricingFormulaConfigId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_pricingformulaconfigs overwrites UpdatedAt
        // on every UPDATE (the soft-delete itself) — needed so EF reads back the trigger-written
        // value instead of keeping the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.SetByUser)
            .WithMany()
            .HasForeignKey(x => x.SetByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DeletedByUser)
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Not unique — multiple historical rows are expected. Supports the "current configuration"
        // lookup (latest EffectiveFrom, not soft-deleted).
        builder.HasIndex(x => x.EffectiveFrom);

        builder.ToTable(t =>
        {
            // Unlike FuelPriceRate.PricePerLitre, all five values here can legitimately be zero
            // (e.g. no margin, no maintenance allowance), so these are >= 0, not strictly positive.
            t.HasCheckConstraint("ck_pfc_base_fare_bounds", "\"BaseFare\" >= 0");
            t.HasCheckConstraint("ck_pfc_rate_per_kg_bounds", "\"RatePerKg\" >= 0");
            t.HasCheckConstraint("ck_pfc_driver_cost_bounds", "\"DriverCostPerKm\" >= 0");
            t.HasCheckConstraint("ck_pfc_maintenance_allowance_bounds", "\"MaintenanceAllowancePerKm\" >= 0");
            t.HasCheckConstraint("ck_pfc_margin_percent_bounds", "\"MarginPercent\" >= 0");
        });
    }
}
