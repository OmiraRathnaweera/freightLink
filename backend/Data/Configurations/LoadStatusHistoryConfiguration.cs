using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class LoadStatusHistoryConfiguration : IEntityTypeConfiguration<LoadStatusHistory>
{
    public void Configure(EntityTypeBuilder<LoadStatusHistory> builder)
    {
        builder.HasKey(x => x.LoadStatusHistoryId);
        builder.Property(x => x.LoadStatusHistoryId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(x => x.Load)
            .WithMany(l => l.StatusHistory)
            .HasForeignKey(x => x.LoadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_lsh_transition", "\"FromStatus\" IS DISTINCT FROM \"ToStatus\"");
            t.HasCheckConstraint("ck_lsh_cancel_reason",
                "\"ToStatus\" <> 'Cancelled' OR \"Reason\" IS NOT NULL");
        });
    }
}
