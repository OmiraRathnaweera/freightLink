using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class DisputeResolutionConfiguration : IEntityTypeConfiguration<DisputeResolution>
{
    public void Configure(EntityTypeBuilder<DisputeResolution> builder)
    {
        builder.HasKey(x => x.DisputeId);

        builder.HasOne(x => x.Dispute)
            .WithOne(d => d.Resolution)
            .HasForeignKey<DisputeResolution>(x => x.DisputeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResolvedByUser)
            .WithMany()
            .HasForeignKey(x => x.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
