using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_user_email");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        // ValueGeneratedOnAddOrUpdate: trg_set_updated_at_users overwrites UpdatedAt on
        // every UPDATE — needed so EF reads back the trigger-written value instead of keeping
        // the stale in-memory one after SaveChanges.
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.Property(x => x.EmailVerificationTokenHash).HasMaxLength(128);
        builder.Property(x => x.PasswordResetTokenHash).HasMaxLength(128);

        builder.HasIndex(x => new { x.EmailVerificationTokenHash, x.EmailVerificationTokenExpiresAt })
            .HasDatabaseName("ix_user_email_verification_token")
            .HasFilter("\"EmailVerificationTokenHash\" IS NOT NULL");
        builder.HasIndex(x => new { x.PasswordResetTokenHash, x.PasswordResetTokenExpiresAt })
            .HasDatabaseName("ix_user_password_reset_token")
            .HasFilter("\"PasswordResetTokenHash\" IS NOT NULL");

        builder.ToTable(t => t.HasCheckConstraint("ck_user_email_format",
            "\"Email\" ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$'"));
        builder.ToTable(t => t.HasCheckConstraint("ck_user_phone_e164",
            "\"PhoneE164\" IS NULL OR \"PhoneE164\" ~ '^\\+[1-9][0-9]{1,14}$'"));
    }
}
