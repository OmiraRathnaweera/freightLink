using System.Net;

namespace FreightLink.Api.Common.Email;

/// <summary>
/// Builds the subject/HTML/plain-text content for the three ADR-012/ADR-018 transactional email
/// templates. Pure string composition — no external template engine, no I/O — so every method here
/// is directly unit-testable.
/// </summary>
public static class EmailTemplates
{
    /// <summary>Builds a one-time email-address verification message.</summary>
    public static (string Subject, string HtmlBody, string TextBody) BuildEmailVerification(string fullName, string verificationUrl)
    {
        const string subject = "Verify your FreightLink email address";
        var bodyHtml = $"""
            <p>Hi {Encode(fullName)},</p>
            <p>Welcome to FreightLink. Verify your email address to activate your account.</p>
            <p><a href="{Encode(verificationUrl)}">Verify email address</a></p>
            <p>This link expires in 24 hours. If you did not create this account, you can safely ignore this email.</p>
            """;
        var bodyText = $"Hi {fullName},\n\nVerify your FreightLink email address: {verificationUrl}\n\nThis link expires in 24 hours. If you did not create this account, you can safely ignore this email.";
        return (subject, Wrap(subject, bodyHtml), bodyText);
    }

    /// <summary>Builds a one-time password-reset message.</summary>
    public static (string Subject, string HtmlBody, string TextBody) BuildPasswordReset(string fullName, string resetUrl)
    {
        const string subject = "Reset your FreightLink password";
        var bodyHtml = $"""
            <p>Hi {Encode(fullName)},</p>
            <p>We received a request to reset your FreightLink password.</p>
            <p><a href="{Encode(resetUrl)}">Choose a new password</a></p>
            <p>This link expires in one hour. If you did not request a password reset, you can safely ignore this email.</p>
            """;
        var bodyText = $"Hi {fullName},\n\nReset your FreightLink password: {resetUrl}\n\nThis link expires in one hour. If you did not request a password reset, you can safely ignore this email.";
        return (subject, Wrap(subject, bodyHtml), bodyText);
    }

    /// <summary>Builds the "agency declined, we're finding another" email.</summary>
    /// <param name="shipperName">The recipient Shipper's display name.</param>
    /// <param name="loadReference">The declined load's reference code.</param>
    /// <param name="agencyName">The agency that declined.</param>
    /// <param name="attemptNo">Which retry attempt this is (1-based).</param>
    /// <returns>The subject, HTML body, and plain-text body for the message.</returns>
    public static (string Subject, string HtmlBody, string TextBody) BuildAgencyDeclined(
        string shipperName, string loadReference, string agencyName, int attemptNo)
    {
        var subject = $"Load {loadReference}: {agencyName} declined — we're finding another agency";
        var bodyHtml = $"""
            <p>Hi {Encode(shipperName)},</p>
            <p><strong>{Encode(agencyName)}</strong> was unable to accept load
            <strong>{Encode(loadReference)}</strong> (attempt #{attemptNo}).</p>
            <p>We're automatically searching for another available agency for this load. You don't need
            to do anything right now — we'll email you again as soon as a new match is found.</p>
            """;
        var bodyText =
            $"Hi {shipperName},\n\n" +
            $"{agencyName} was unable to accept load {loadReference} (attempt #{attemptNo}).\n\n" +
            "We're automatically searching for another available agency for this load. You don't need " +
            "to do anything right now — we'll email you again as soon as a new match is found.";

        return (subject, Wrap(subject, bodyHtml), bodyText);
    }

    /// <summary>Builds the "new match found, please review" email.</summary>
    /// <param name="shipperName">The recipient Shipper's display name.</param>
    /// <param name="loadReference">The matched load's reference code.</param>
    /// <param name="agencyName">The newly matched agency.</param>
    /// <returns>The subject, HTML body, and plain-text body for the message.</returns>
    public static (string Subject, string HtmlBody, string TextBody) BuildNewMatchFound(
        string shipperName, string loadReference, string agencyName)
    {
        var subject = $"Load {loadReference}: new match found — please review";
        var bodyHtml = $"""
            <p>Hi {Encode(shipperName)},</p>
            <p>We found a new match for load <strong>{Encode(loadReference)}</strong>:
            <strong>{Encode(agencyName)}</strong>.</p>
            <p>Please open the FreightLink app to review and approve this match.</p>
            """;
        var bodyText =
            $"Hi {shipperName},\n\n" +
            $"We found a new match for load {loadReference}: {agencyName}.\n\n" +
            "Please open the FreightLink app to review and approve this match.";

        return (subject, Wrap(subject, bodyHtml), bodyText);
    }

    /// <summary>Builds the "no automatic match found, please check the app" email.</summary>
    /// <param name="shipperName">The recipient Shipper's display name.</param>
    /// <param name="loadReference">The unmatched load's reference code.</param>
    /// <returns>The subject, HTML body, and plain-text body for the message.</returns>
    public static (string Subject, string HtmlBody, string TextBody) BuildNoAutomaticMatchFound(
        string shipperName, string loadReference)
    {
        var subject = $"Load {loadReference}: no automatic match found";
        var bodyHtml = $"""
            <p>Hi {Encode(shipperName)},</p>
            <p>We weren't able to automatically match load <strong>{Encode(loadReference)}</strong> with
            an agency.</p>
            <p>Please open the FreightLink app to review this load and choose how you'd like to proceed.</p>
            """;
        var bodyText =
            $"Hi {shipperName},\n\n" +
            $"We weren't able to automatically match load {loadReference} with an agency.\n\n" +
            "Please open the FreightLink app to review this load and choose how you'd like to proceed.";

        return (subject, Wrap(subject, bodyHtml), bodyText);
    }

    /// <summary>Wraps a template's inner HTML in a small, shared, professional-looking shell.</summary>
    private static string Wrap(string title, string innerHtml) => $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><title>{Encode(title)}</title></head>
        <body style="font-family: Arial, Helvetica, sans-serif; color: #1a1a1a; line-height: 1.5;">
          <div style="max-width: 480px; margin: 0 auto; padding: 24px;">
            <h2 style="color: #0b3d91; margin-bottom: 16px;">FreightLink</h2>
            {innerHtml}
            <p style="margin-top: 32px; font-size: 12px; color: #6b6b6b;">
              This is an automated message from FreightLink. Please do not reply directly to this email.
            </p>
          </div>
        </body>
        </html>
        """;

    /// <summary>HTML-encodes interpolated, user-supplied values before they're placed into a body.</summary>
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
