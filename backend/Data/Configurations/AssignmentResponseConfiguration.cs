using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class AssignmentResponseConfiguration : IEntityTypeConfiguration<AssignmentResponse>
{
    public void Configure(EntityTypeBuilder<AssignmentResponse> builder)
    {
        builder.HasKey(x => x.AssignmentId);

        builder.HasOne(x => x.Assignment)
            .WithOne(a => a.Response)
            .HasForeignKey<AssignmentResponse>(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RespondedByUser)
            .WithMany()
            .HasForeignKey(x => x.RespondedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("ck_ar_decline_reason",
            "\"Response\" <> 'Declined' OR \"DeclineReason\" IS NOT NULL"));
    }
}
