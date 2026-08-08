using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class AgencyStatusHistoryConfiguration : IEntityTypeConfiguration<AgencyStatusHistory>
{
    public void Configure(EntityTypeBuilder<AgencyStatusHistory> builder)
    {
        builder.HasKey(x => x.AgencyStatusHistoryId);
        builder.Property(x => x.AgencyStatusHistoryId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(x => x.Agency)
            .WithMany(a => a.StatusHistory)
            .HasForeignKey(x => x.AgencyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
