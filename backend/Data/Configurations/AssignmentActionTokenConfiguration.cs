using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class AssignmentActionTokenConfiguration : IEntityTypeConfiguration<AssignmentActionToken>
{
    public void Configure(EntityTypeBuilder<AssignmentActionToken> builder)
    {
        builder.HasKey(x => x.AssignmentActionTokenId);
        builder.Property(x => x.AssignmentActionTokenId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_assignmentactiontoken_hash");

        builder.HasIndex(x => new { x.AssignmentId, x.ConsumedAt })
            .HasDatabaseName("ix_assignmentactiontoken_assignment_active");

        builder.HasOne(x => x.Assignment)
            .WithMany()
            .HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ActingUser)
            .WithMany()
            .HasForeignKey(x => x.ActingUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("ck_assignmentactiontoken_expiry", "\"ExpiresAt\" > \"CreatedAt\""));
    }
}
