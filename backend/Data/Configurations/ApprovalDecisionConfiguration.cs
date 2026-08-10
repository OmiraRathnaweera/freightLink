using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.HasKey(x => x.ApprovalDecisionId);
        builder.Property(x => x.ApprovalDecisionId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(x => x.WorkflowRun)
            .WithMany(w => w.ApprovalDecisions)
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DecidedByUser)
            .WithMany()
            .HasForeignKey(x => x.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.WorkflowRunId, x.SequenceNo })
            .IsUnique()
            .HasDatabaseName("uq_ad_sequence");

        builder.HasIndex(x => x.WorkflowRunId)
            .IsUnique()
            .HasDatabaseName("ux_ad_single_approve")
            .HasFilter("\"Decision\" = 'Approve'");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_ad_sequence", "\"SequenceNo\" >= 1");
            t.HasCheckConstraint("ck_ad_reason", "\"Decision\" = 'Approve' OR \"Reason\" IS NOT NULL");
        });
    }
}
