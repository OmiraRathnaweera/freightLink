using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class PaymentWebhookEvent
{
    public Guid PaymentWebhookEventId { get; set; }
    public string GatewayRef { get; set; } = string.Empty;
    public string RawPayloadHash { get; set; } = string.Empty;
    public bool SignatureValid { get; set; }
    public WebhookProcessingStatus ProcessingStatus { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
