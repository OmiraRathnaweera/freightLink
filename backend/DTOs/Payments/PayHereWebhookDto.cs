using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.DTOs.Payments;

/// <summary>
/// Data transfer object binding the asynchronous server-to-server IPN / webhook callback from PayHere.
/// Sent with content-type application/x-www-form-urlencoded.
/// </summary>
public class PayHereWebhookDto
{
    [FromForm(Name = "merchant_id")]
    [JsonPropertyName("merchant_id")]
    public string? MerchantId { get; set; }

    [FromForm(Name = "order_id")]
    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [FromForm(Name = "payment_id")]
    [JsonPropertyName("payment_id")]
    public string? PaymentId { get; set; }

    [FromForm(Name = "payhere_amount")]
    [JsonPropertyName("payhere_amount")]
    public string? PayhereAmount { get; set; }

    [FromForm(Name = "payhere_currency")]
    [JsonPropertyName("payhere_currency")]
    public string? PayhereCurrency { get; set; }

    [FromForm(Name = "status_code")]
    [JsonPropertyName("status_code")]
    public string? StatusCode { get; set; }

    [FromForm(Name = "md5sig")]
    [JsonPropertyName("md5sig")]
    public string? Md5Sig { get; set; }

    [FromForm(Name = "custom_1")]
    [JsonPropertyName("custom_1")]
    public string? Custom1 { get; set; }

    [FromForm(Name = "custom_2")]
    [JsonPropertyName("custom_2")]
    public string? Custom2 { get; set; }

    [FromForm(Name = "status_message")]
    [JsonPropertyName("status_message")]
    public string? StatusMessage { get; set; }

    [FromForm(Name = "method")]
    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [FromForm(Name = "card_holder_name")]
    [JsonPropertyName("card_holder_name")]
    public string? CardHolderName { get; set; }

    [FromForm(Name = "card_no")]
    [JsonPropertyName("card_no")]
    public string? CardNo { get; set; }

    [FromForm(Name = "card_expiry")]
    [JsonPropertyName("card_expiry")]
    public string? CardExpiry { get; set; }
}
