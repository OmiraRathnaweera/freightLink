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
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

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
            t.HasCheckConstraint("ck_awr_attempt", "\"AttemptNo\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("ck_awr_completed", "\"CompletedAt\" IS NULL OR \"CompletedAt\" >= \"StartedAt\"");
        });
    }
}
