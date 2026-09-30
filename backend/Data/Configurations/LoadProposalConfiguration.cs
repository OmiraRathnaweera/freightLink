using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class LoadProposalConfiguration : IEntityTypeConfiguration<LoadProposal>
{
    public void Configure(EntityTypeBuilder<LoadProposal> builder)
    {
        builder.HasKey(x => x.LoadProposalId);
        builder.Property(x => x.LoadProposalId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ProposedPrice).HasPrecision(12, 2);
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.Property(x => x.ResponseReason).HasMaxLength(1000);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne(x => x.Load)
            .WithMany(l => l.Proposals)
            .HasForeignKey(x => x.LoadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Agency)
            .WithMany()
            .HasForeignKey(x => x.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProposedByUser)
            .WithMany()
            .HasForeignKey(x => x.ProposedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One live (Pending) proposal per agency per load — an agency withdraws or waits for a
        // response before it can submit another for the same load.
        builder.HasIndex(x => new { x.LoadId, x.AgencyId })
            .IsUnique()
            .HasDatabaseName("ux_loadproposal_live_per_load_agency")
            .HasFilter("\"Status\" = 'Pending'");

        builder.ToTable(t => t.HasCheckConstraint("ck_loadproposal_price", "\"ProposedPrice\" > 0"));
    }
}
