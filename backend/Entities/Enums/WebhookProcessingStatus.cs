namespace FreightLink.Api.Entities.Enums;

public enum WebhookProcessingStatus
{
    Received,
    SignatureRejected,
    Duplicate,
    Applied,
    Error
}
