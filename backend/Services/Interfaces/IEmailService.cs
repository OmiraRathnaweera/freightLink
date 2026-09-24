using FreightLink.Api.Common.Email;

namespace FreightLink.Api.Services.Interfaces;

/// <summary>
/// Reusable, internal-only transactional email sender (ADR-012/ADR-018). Not exposed through any
/// controller — other services call this directly (e.g. the agency decline/retry flow, the matching
/// flow) once those are built. Behavior is governed entirely by <see cref="Common.Options.EmailOptions"/>:
/// disabled or sandboxed sends never touch the network — see <see cref="Services.GmailEmailService"/>.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends (or, depending on configuration, no-ops/logs) an arbitrary <see cref="EmailMessage"/>.
    /// The three typed helpers below are the preferred entry points for the ADR-specified templates;
    /// this exists for the underlying primitive and for any future template not yet covered by one.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="Common.Exceptions.ApiException">
    /// 500 <see cref="Common.Errors.ErrorCode.EMAIL_SEND_FAILED"/> if the SMTP send fails. Never thrown
    /// when email sending is disabled or sandboxed.
    /// </exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>Sends the "agency declined, we're finding another" template.</summary>
    /// <param name="toEmail">Recipient Shipper's email address.</param>
    /// <param name="shipperName">Recipient Shipper's display name.</param>
    /// <param name="loadReference">The declined load's reference code.</param>
    /// <param name="agencyName">The agency that declined.</param>
    /// <param name="attemptNo">Which retry attempt this is (1-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAgencyDeclinedAsync(string toEmail, string shipperName, string loadReference, string agencyName, int attemptNo, CancellationToken cancellationToken = default);

    /// <summary>Sends the "new match found, please review" template.</summary>
    /// <param name="toEmail">Recipient Shipper's email address.</param>
    /// <param name="shipperName">Recipient Shipper's display name.</param>
    /// <param name="loadReference">The matched load's reference code.</param>
    /// <param name="agencyName">The newly matched agency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendNewMatchFoundAsync(string toEmail, string shipperName, string loadReference, string agencyName, CancellationToken cancellationToken = default);

    /// <summary>Sends the "no automatic match found, please check the app" template.</summary>
    /// <param name="toEmail">Recipient Shipper's email address.</param>
    /// <param name="shipperName">Recipient Shipper's display name.</param>
    /// <param name="loadReference">The unmatched load's reference code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendNoAutomaticMatchFoundAsync(string toEmail, string shipperName, string loadReference, CancellationToken cancellationToken = default);
}
