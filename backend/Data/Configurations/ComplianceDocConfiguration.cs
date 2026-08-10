using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class ComplianceDocConfiguration : IEntityTypeConfiguration<ComplianceDoc>
{
    public void Configure(EntityTypeBuilder<ComplianceDoc> builder)
    {
        builder.HasKey(x => x.ComplianceDocId);
        builder.Property(x => x.ComplianceDocId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_compliancedocs overwrites UpdatedAt on
        // every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.Agency)
            .WithMany(a => a.ComplianceDocs)
            .HasForeignKey(x => x.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AgencyId, x.DocType })
            .HasDatabaseName("ux_compliancedoc_live")
            .IsUnique()
            .HasFilter("\"Status\" IN ('Pending','Verified')");

        builder.ToTable(t => t.HasCheckConstraint("ck_compliancedoc_dates",
            "\"ExpiresOn\" IS NULL OR \"ExpiresOn\" > \"IssuedOn\""));
    }
}
