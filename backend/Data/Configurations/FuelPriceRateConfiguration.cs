using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

/// <summary>EF Core mapping for <see cref="FuelPriceRate"/>.</summary>
public class FuelPriceRateConfiguration : IEntityTypeConfiguration<FuelPriceRate>
{
    /// <summary>Configures keys, FKs, indexes, and check constraints for <see cref="FuelPriceRate"/>.</summary>
    public void Configure(EntityTypeBuilder<FuelPriceRate> builder)
    {
        builder.HasKey(x => x.FuelPriceRateId);
        builder.Property(x => x.FuelPriceRateId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_fuelpricerates overwrites UpdatedAt on
        // every UPDATE (the soft-delete itself) — needed so EF reads back the trigger-written value
        // instead of keeping the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.SetByUser)
            .WithMany()
            .HasForeignKey(x => x.SetByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DeletedByUser)
            .WithMany()
            .HasForeignKey(x => x.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Not unique — multiple historical rows per FuelType are expected. Supports the "current
        // rate for this fuel type" lookup (latest EffectiveFrom, not soft-deleted).
        builder.HasIndex(x => new { x.FuelType, x.EffectiveFrom });

        builder.ToTable(t => t.HasCheckConstraint("ck_fpr_price_positive", "\"PricePerLitre\" > 0"));
    }
}
