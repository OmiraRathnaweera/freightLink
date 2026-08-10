using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.HasKey(x => x.AssignmentId);
        builder.Property(x => x.AssignmentId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ProposedPrice).HasPrecision(12, 2);
        builder.Property(x => x.RoutedDistanceKm).HasPrecision(10, 2);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_assignments overwrites UpdatedAt on
        // every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.Load)
            .WithMany(l => l.Assignments)
            .HasForeignKey(x => x.LoadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Agency)
            .WithMany(a => a.Assignments)
            .HasForeignKey(x => x.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WorkflowRun)
            .WithMany(w => w.Assignments)
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.LoadId)
            .IsUnique()
            .HasDatabaseName("ux_assignment_live_per_load")
            .HasFilter("\"Status\" IN ('Proposed','Accepted')");

        builder.HasIndex(x => new { x.LoadId, x.AgencyId })
            .IsUnique()
            .HasDatabaseName("ux_assignment_load_agency");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_assignment_price", "\"ProposedPrice\" > 0");
            t.HasCheckConstraint("ck_assignment_distance", "\"RoutedDistanceKm\" IS NULL OR \"RoutedDistanceKm\" > 0");
            t.HasCheckConstraint("ck_assignment_eta", "\"ProposedEtaMinutes\" IS NULL OR \"ProposedEtaMinutes\" > 0");
        });
    }
}
