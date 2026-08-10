using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class AgentWorkflowRunConfiguration : IEntityTypeConfiguration<AgentWorkflowRun>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowRun> builder)
    {
        builder.HasKey(x => x.WorkflowRunId);
        builder.Property(x => x.WorkflowRunId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.PlanJson).HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_agentworkflowruns overwrites UpdatedAt
        // on every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.Load)
            .WithMany(l => l.WorkflowRuns)
            .HasForeignKey(x => x.LoadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TriggeredByUser)
            .WithMany()
            .HasForeignKey(x => x.TriggeredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.LoadId, x.AttemptNo })
            .IsUnique()
            .HasDatabaseName("uq_awr_load_attempt");

        builder.HasIndex(x => x.StartedAt)
            .HasDatabaseName("ix_awr_awaiting")
            .HasFilter("\"Status\" = 'AwaitingApproval'");

        builder.ToTable(t =>
        {
            // Positive-attempt invariant only: the maximum retry count is application-configurable
            // (workflow orchestration's concern, not a fixed schema fact), so the DB does not pin
            // an upper bound here — matches ck_payment_attempt/ck_toolcall_attempt, which likewise
            // only enforce >= 1.
            t.HasCheckConstraint("ck_awr_attempt", "\"AttemptNo\" >= 1");
            t.HasCheckConstraint("ck_awr_completed", "\"CompletedAt\" IS NULL OR \"CompletedAt\" >= \"StartedAt\"");
        });
    }
}
