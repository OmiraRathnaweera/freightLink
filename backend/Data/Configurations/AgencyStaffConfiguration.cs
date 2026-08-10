using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class AgencyStaffConfiguration : IEntityTypeConfiguration<AgencyStaff>
{
    public void Configure(EntityTypeBuilder<AgencyStaff> builder)
    {
        builder.HasKey(x => x.UserId);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.User)
            .WithOne(u => u.AgencyStaff)
            .HasForeignKey<AgencyStaff>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Agency)
            .WithMany(a => a.Staff)
            .HasForeignKey(x => x.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
