using System.Text.Json.Serialization;

namespace FreightLink.Api.DTOs.Payments;

/// <summary>
/// Checkout initiation parameters returned to the frontend to trigger PayHere Hosted Checkout.
/// Includes all required checkout form fields and the pre-computed MD5 security hash.
/// </summary>
public class PayHereCheckoutResponseDto
{
    /// <summary>The PayHere checkout endpoint to submit the payment request to.</summary>
    [JsonPropertyName("actionUrl")]
    public string ActionUrl { get; set; } = string.Empty;

    /// <summary>PayHere merchant identifier.</summary>
    [JsonPropertyName("merchantId")]
    public string MerchantId { get; set; } = string.Empty;

    /// <summary>Redirect URL on successful payment.</summary>
    [JsonPropertyName("returnUrl")]
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>Redirect URL if the payer cancels the checkout.</summary>
    [JsonPropertyName("cancelUrl")]
    public string CancelUrl { get; set; } = string.Empty;

    /// <summary>Server-to-server IPN callback URL.</summary>
    [JsonPropertyName("notifyUrl")]
    public string NotifyUrl { get; set; } = string.Empty;

    /// <summary>Unique order identifier (Invoice ID).</summary>
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>Brief description of invoice items.</summary>
    [JsonPropertyName("items")]
    public string Items { get; set; } = string.Empty;

    /// <summary>Three-letter ISO currency code, strictly "LKR".</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "LKR";

    /// <summary>Formatted amount with strictly two decimal places, e.g. "1500.00".</summary>
    [JsonPropertyName("amount")]
    public string Amount { get; set; } = string.Empty;

    /// <summary>MD5 checkout verification hash generated with merchant secret.</summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    /// <summary>Shipper's first name.</summary>
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Shipper's last name.</summary>
    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Shipper's contact email address.</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Shipper's telephone or mobile number.</summary>
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Shipper's billing address line.</summary>
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    /// <summary>Shipper's billing city.</summary>
    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    /// <summary>Shipper's billing country.</summary>
    [JsonPropertyName("country")]
    public string Country { get; set; } = "Sri Lanka";

    /// <summary>Associated invoice internal ID for client navigation.</summary>
    [JsonPropertyName("invoiceId")]
    public Guid InvoiceId { get; set; }

    /// <summary>Human-readable invoice reference number.</summary>
    [JsonPropertyName("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;
}
