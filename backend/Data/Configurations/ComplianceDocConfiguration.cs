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
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.Agency)
            .WithMany(a => a.ComplianceDocs)
            .HasForeignKey(x => x.AgencyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
