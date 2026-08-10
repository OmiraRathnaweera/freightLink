using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class AgentStepConfiguration : IEntityTypeConfiguration<AgentStep>
{
    public void Configure(EntityTypeBuilder<AgentStep> builder)
    {
        builder.HasKey(x => x.AgentStepId);
        builder.Property(x => x.AgentStepId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.InputJson).HasColumnType("jsonb");
        builder.Property(x => x.OutputJson).HasColumnType("jsonb");

        builder.HasOne(x => x.WorkflowRun)
            .WithMany(w => w.Steps)
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.WorkflowRunId, x.StepNo })
            .IsUnique()
            .HasDatabaseName("uq_agentstep_order");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_agentstep_stepno", "\"StepNo\" >= 1");
            t.HasCheckConstraint("ck_agentstep_failure", "\"Status\" <> 'Failed' OR \"ErrorMessage\" IS NOT NULL");
        });
    }
}
