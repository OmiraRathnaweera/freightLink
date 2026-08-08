using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Notification
{
    public Guid NotificationId { get; set; }
    public Guid RecipientUserId { get; set; }
    public Guid LoadId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationDeliveryStatus DeliveryStatus { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User RecipientUser { get; set; } = null!;
    public Load Load { get; set; } = null!;
}
