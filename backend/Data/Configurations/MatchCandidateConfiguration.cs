using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class MatchCandidateConfiguration : IEntityTypeConfiguration<MatchCandidate>
{
    public void Configure(EntityTypeBuilder<MatchCandidate> builder)
    {
        builder.HasKey(x => x.MatchCandidateId);
        builder.Property(x => x.MatchCandidateId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EligibilityScore).HasPrecision(5, 2);

        builder.HasOne(x => x.WorkflowRun)
            .WithMany(w => w.MatchCandidates)
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Agency)
            .WithMany(a => a.MatchCandidates)
            .HasForeignKey(x => x.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.WorkflowRunId, x.AgencyId })
            .IsUnique()
            .HasDatabaseName("uq_mc_run_agency");

        builder.HasIndex(x => new { x.WorkflowRunId, x.Rank })
            .IsUnique()
            .HasDatabaseName("ux_mc_run_rank")
            .HasFilter("\"Eligible\" = true");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_mc_score", "\"EligibilityScore\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_mc_reason", "\"Eligible\" = true OR \"RejectionReason\" IS NOT NULL");
        });
    }
}
