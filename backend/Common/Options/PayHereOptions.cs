namespace FreightLink.Api.Common.Options;

/// <summary>
/// Configuration settings for the PayHere payment gateway (Sandbox or Production).
/// Bound from the "PayHere" configuration section or individual PAYHERE_* environment variables.
/// </summary>
public class PayHereOptions
{
    public const string SectionName = "PayHere";

    /// <summary>PayHere Merchant ID issued by PayHere.</summary>
    public string MerchantId { get; set; } = string.Empty;

    /// <summary>PayHere Merchant Secret used for MD5 hash generation and signature verification.</summary>
    public string MerchantSecret { get; set; } = string.Empty;

    /// <summary>Target environment: "sandbox" or "live". Defaults to "sandbox".</summary>
    public string Env { get; set; } = "sandbox";

    /// <summary>Hosted checkout endpoint URL. Defaults to the PayHere sandbox checkout URL.</summary>
    public string BaseUrl { get; set; } = "https://sandbox.payhere.lk/pay/checkout";

    /// <summary>Frontend URL where payer is redirected upon approved payment.</summary>
    public string ReturnUrl { get; set; } = "http://localhost:5173/invoices/{id}/payment-success";

    /// <summary>Frontend URL where payer is redirected if checkout is cancelled.</summary>
    public string CancelUrl { get; set; } = "http://localhost:5173/invoices/{id}/payment-cancelled";

    /// <summary>Publicly accessible webhook callback URL for server-to-server IPN notifications.</summary>
    public string NotifyUrl { get; set; } = "http://localhost:5159/api/payments/payhere/notify";

    /// <summary>
    /// Gets the active checkout URL, defaulting to sandbox unless configured for live.
    /// </summary>
    public string GetEffectiveCheckoutUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl;
        }

        return string.Equals(Env, "live", StringComparison.OrdinalIgnoreCase)
            ? "https://www.payhere.lk/pay/checkout"
            : "https://sandbox.payhere.lk/pay/checkout";
    }
}
