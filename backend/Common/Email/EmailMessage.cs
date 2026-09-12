using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Common.Email;

/// <summary>
/// A single outbound email, ready to hand to <see cref="Services.Interfaces.IEmailService"/>'s
/// generic <c>SendAsync</c>. Typically built by <see cref="EmailTemplates"/> rather than constructed
/// directly by callers.
/// </summary>
public class EmailMessage
{
    /// <summary>Recipient email address.</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>Email subject line.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>HTML body.</summary>
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>Plain-text fallback body, for clients that don't render HTML.</summary>
    public string TextBody { get; set; } = string.Empty;

    /// <summary>
    /// Which of the ADR-012/ADR-018 templates this message represents, if any. Reuses
    /// <see cref="NotificationCategory"/> rather than a dedicated enum, since that type already has a
    /// member for each of this feature's three templates. Informational/logging only — it does not
    /// drive any branching in <see cref="Services.GmailEmailService"/> itself.
    /// </summary>
    public NotificationCategory? TemplateKey { get; set; }

    /// <summary>Optional free-form context (e.g. load reference, agency name) attached for logging.</summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
