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
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.ToTable(t => t.HasCheckConstraint("ck_user_email_format",
            "\"Email\" ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$'"));
        builder.ToTable(t => t.HasCheckConstraint("ck_user_phone_e164",
            "\"PhoneE164\" IS NULL OR \"PhoneE164\" ~ '^\\+[1-9][0-9]{1,14}$'"));
    }
}
