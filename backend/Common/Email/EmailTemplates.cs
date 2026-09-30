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

    /// <summary>Builds the "your agency created a driver account for you" credentials email.</summary>
    /// <param name="fullName">The new driver's display name.</param>
    /// <param name="email">The driver's login email (also the recipient).</param>
    /// <param name="temporaryPassword">The server-generated temporary password, in plain text.</param>
    /// <param name="agencyName">The employing agency's display name.</param>
    /// <param name="loginUrl">Absolute URL to the FreightLink sign-in page.</param>
    public static (string Subject, string HtmlBody, string TextBody) BuildDriverCredentials(
        string fullName, string email, string temporaryPassword, string agencyName, string loginUrl)
    {
        const string subject = "Your FreightLink driver account is ready";
        var bodyHtml = $"""
            <p>Hi {Encode(fullName)},</p>
            <p><strong>{Encode(agencyName)}</strong> has added you as a driver on FreightLink. Use the credentials
            below to sign in from the FreightLink mobile app:</p>
            <p>
              Email: <strong>{Encode(email)}</strong><br>
              Temporary password: <strong>{Encode(temporaryPassword)}</strong>
            </p>
            <p><a href="{Encode(loginUrl)}">Open FreightLink</a></p>
            <p>For your security, please change this password after you first sign in.</p>
            """;
        var bodyText =
            $"Hi {fullName},\n\n" +
            $"{agencyName} has added you as a driver on FreightLink. Use the credentials below to sign in " +
            "from the FreightLink mobile app:\n\n" +
            $"Email: {email}\n" +
            $"Temporary password: {temporaryPassword}\n\n" +
            $"Sign in: {loginUrl}\n\n" +
            "For your security, please change this password after you first sign in.";

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

    /// <summary>Builds the agency-facing job-proposal message. When <paramref name="acceptUrl"/>
    /// and <paramref name="declineUrl"/> are both supplied, the email carries direct Accept/
    /// Decline action buttons (single-use, 7-day links - see AssignmentActionTokenService);
    /// otherwise it falls back to the original "log in to your portal" copy.</summary>
    public static (string Subject, string HtmlBody, string TextBody) BuildJobProposal(
        string agencyName,
        string loadReference,
        string cargoDescription,
        decimal weightKg,
        string pickupAddress,
        string dropoffAddress,
        decimal proposedPrice,
        string? acceptUrl,
        string? declineUrl)
    {
        var subject = $"FreightLink — New Job Proposal for Load #{loadReference}";
        var hasActionLinks = !string.IsNullOrWhiteSpace(acceptUrl) && !string.IsNullOrWhiteSpace(declineUrl);

        var actionHtml = hasActionLinks
            ? $"""
                <p style="margin: 24px 0;">
                  <a href="{Encode(acceptUrl!)}" style="display:inline-block;padding:10px 20px;margin-right:12px;background:#0b8a3e;color:#fff;text-decoration:none;border-radius:4px;font-weight:bold;">Accept proposal</a>
                  <a href="{Encode(declineUrl!)}" style="display:inline-block;padding:10px 20px;background:#b3261e;color:#fff;text-decoration:none;border-radius:4px;font-weight:bold;">Decline proposal</a>
                </p>
                <p style="font-size:12px;color:#6b6b6b;">These links expire in 7 days and work without logging in first. You can also respond from the FreightLink Agency Portal.</p>
                """
            : "<p>Please log in to your FreightLink Agency portal to accept or decline this proposal.</p>";

        var bodyHtml = $"""
            <p>Dear {Encode(agencyName)},</p>
            <p>A new freight load proposal has been matched and assigned to your agency on FreightLink.</p>
            <ul>
                <li><strong>Load Reference:</strong> {Encode(loadReference)}</li>
                <li><strong>Cargo:</strong> {Encode(cargoDescription)} ({weightKg:N0} kg)</li>
                <li><strong>Pickup Location:</strong> {Encode(pickupAddress)}</li>
                <li><strong>Dropoff Location:</strong> {Encode(dropoffAddress)}</li>
                <li><strong>Proposed Price:</strong> LKR {proposedPrice:N2}</li>
            </ul>
            {actionHtml}
            """;

        var actionText = hasActionLinks
            ? $"Accept: {acceptUrl}\nDecline: {declineUrl}\n\n(Links expire in 7 days and work without logging in first; you can also respond from the FreightLink Agency Portal.)"
            : "Please log in to your FreightLink Agency portal to accept or decline this proposal.";

        var bodyText =
            $"Dear {agencyName},\n\n" +
            "A new freight load proposal has been assigned to your agency.\n" +
            $"Load Reference: {loadReference}\n" +
            $"Cargo: {cargoDescription} ({weightKg:N0} kg)\n" +
            $"Pickup Location: {pickupAddress}\n" +
            $"Dropoff Location: {dropoffAddress}\n" +
            $"Proposed Price: LKR {proposedPrice:N2}\n\n" +
            actionText;

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
