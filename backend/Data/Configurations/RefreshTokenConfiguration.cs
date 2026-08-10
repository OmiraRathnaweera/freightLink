using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(x => x.RefreshTokenId);
        builder.Property(x => x.RefreshTokenId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_refreshtoken_hash");

        builder.HasIndex(x => new { x.UserId, x.ExpiresAt })
            .HasDatabaseName("ix_refreshtoken_user_active")
            .HasFilter("\"RevokedAt\" IS NULL");

        builder.HasOne(x => x.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_refreshtoken_expiry", "\"ExpiresAt\" > \"IssuedAt\"");
            t.HasCheckConstraint("ck_refreshtoken_revoked", "\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"IssuedAt\"");
        });
    }
}
