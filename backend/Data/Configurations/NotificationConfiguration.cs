using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(x => x.NotificationId);
        builder.Property(x => x.NotificationId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.RecipientUser)
            .WithMany()
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Load)
            .WithMany(l => l.Notifications)
            .HasForeignKey(x => x.LoadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RecipientUserId, x.CreatedAt })
            .HasDatabaseName("ix_notification_unread")
            .HasFilter("\"ReadAt\" IS NULL")
            .IsDescending(false, true);

        builder.ToTable(t =>
        {
            // DeliveryStatus is a non-nullable column, so "IS NOT NULL" alone is a tautology.
            // The real invariant this constraint's name implies: an Email notification marked
            // Sent must actually carry delivery evidence (when it was sent, and the provider's
            // tracking id) rather than just flipping a status flag with nothing behind it.
            t.HasCheckConstraint("ck_notification_email_delivery",
                "\"Channel\" <> 'Email' OR \"DeliveryStatus\" <> 'Sent' " +
                "OR (\"SentAt\" IS NOT NULL AND \"ProviderMessageId\" IS NOT NULL)");
            t.HasCheckConstraint("ck_notification_failure",
                "\"DeliveryStatus\" <> 'Failed' OR \"ErrorMessage\" IS NOT NULL");
        });
    }
}
