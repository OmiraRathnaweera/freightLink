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
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

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
    }
}
