namespace FreightLink.Api.Common.Options;

/// <summary>
/// Strongly-typed binding for the "Email" configuration section (env vars <c>EMAIL__*</c>), used by
/// <see cref="Services.GmailEmailService"/> to send transactional email over Gmail SMTP.
/// </summary>
public class EmailOptions
{
    /// <summary>Public React application URL used in account-action links sent by email.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>SMTP server host, e.g. <c>smtp.gmail.com</c>.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP server port, e.g. <c>587</c> for STARTTLS.</summary>
    public int SmtpPort { get; set; }

    /// <summary>Whether to negotiate TLS/SSL on connect. Should be <c>true</c> for Gmail SMTP.</summary>
    public bool EnableSsl { get; set; }

    /// <summary>SMTP auth username, typically the sending Gmail address.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>SMTP auth password (a Gmail app password, not the account password). Never hardcode — env var only.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Display name used as the email "From" header, e.g. "FreightLink".</summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>Sending address used as the email "From" header.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Master feature toggle. When <c>false</c>, <see cref="Services.GmailEmailService"/> no-ops
    /// (logs and returns) instead of sending or requiring any of the other settings above.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When <c>true</c> (and <see cref="Enabled"/> is <c>true</c>), no real SMTP connection is made —
    /// the message that would have been sent is logged instead. For demos/local dev.
    /// </summary>
    public bool SandboxMode { get; set; }

    /// <summary>
    /// Whether newly registered accounts must verify their email before receiving JWTs. Defaults to
    /// true so a missing configuration value does not weaken account verification in production.
    /// </summary>
    public bool RequireEmailVerification { get; set; } = true;

    /// <summary>
    /// Allows a development machine to bypass SMTP certificate validation when its network cannot
    /// complete certificate-revocation checks. This is ignored outside the Development environment
    /// and must never be enabled for staging or production.
    /// </summary>
    public bool AllowInvalidCertificateForDevelopment { get; set; }

    /// <summary>
    /// Validates that every setting required to actually send mail is present. Only meaningful — and
    /// only called — when <see cref="Enabled"/> is <c>true</c>; a disabled email feature is allowed to
    /// have empty settings (mirrors the existing no-op-when-unset precedent for
    /// <see cref="AdminSeedOptions"/>/<see cref="InternalApiOptions"/>), while an enabled-but-misconfigured
    /// one fails fast instead of surfacing as an opaque runtime error on the first send attempt.
    /// </summary>
    /// <exception cref="InvalidOperationException">A required setting is missing.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SmtpHost))
        {
            throw new InvalidOperationException("EMAIL__SMTPHOST is required when EMAIL__ENABLED is true.");
        }

        if (SmtpPort <= 0)
        {
            throw new InvalidOperationException("EMAIL__SMTPPORT must be a positive port number when EMAIL__ENABLED is true.");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException("EMAIL__USERNAME is required when EMAIL__ENABLED is true.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("EMAIL__PASSWORD is required when EMAIL__ENABLED is true.");
        }

        if (string.IsNullOrWhiteSpace(FromAddress))
        {
            throw new InvalidOperationException("EMAIL__FROMADDRESS is required when EMAIL__ENABLED is true.");
        }

        if (!Uri.TryCreate(FrontendBaseUrl, UriKind.Absolute, out var frontendUrl)
            || (frontendUrl.Scheme != Uri.UriSchemeHttp && frontendUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("EMAIL__FRONTENDBASEURL must be an absolute HTTP(S) URL when EMAIL__ENABLED is true.");
        }
    }
}
