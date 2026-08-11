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

        // Optimistic concurrency via Postgres's built-in xmin system column: guarantees that if two
        // concurrent /auth/refresh requests both load the same not-yet-revoked token, only the first
        // to SaveChanges actually revokes it — the second's UPDATE affects zero rows under the hood
        // and EF raises DbUpdateConcurrencyException (translated to INVALID_REFRESH_TOKEN by
        // TokenService.RotateRefreshTokenAsync) instead of both silently succeeding and each minting
        // a successor from the same raw token. A transaction alone doesn't prevent this: both
        // requests can read the row as unrevoked under the default READ COMMITTED isolation before
        // either commits its UPDATE.
        // NOTE: EF Core's provider-agnostic IsRowVersion() shadow-property pattern doesn't work
        // here — Npgsql/EF treats a plain `.Property<uint>("xmin").IsRowVersion()` as a brand-new
        // column to create via migration, which collides with Postgres's actual reserved system
        // column of the same name (confirmed: `dotnet ef migrations add` generated an AddColumn for
        // "xmin", which would fail to apply). UseXminAsConcurrencyToken() is obsolete in this
        // package version but is still the only API that correctly maps to the existing system
        // column (read-only, no DDL) rather than creating a new one — kept deliberately.
#pragma warning disable CS0618 // UseXminAsConcurrencyToken is obsolete; see note above for why the suggested replacement doesn't work for Postgres's real xmin column.
        builder.UseXminAsConcurrencyToken();
#pragma warning restore CS0618

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
