using System.Net;
using Microsoft.Extensions.Hosting;
using FreightLink.Api.Common.Email;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Common.Options;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IEmailService" />
public class GmailEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<GmailEmailService> _logger;
    private readonly IHostEnvironment? _hostEnvironment;

    /// <summary>Creates the service with its DI-provided <see cref="EmailOptions"/> and logger.</summary>
    public GmailEmailService(
        IOptions<EmailOptions> options,
        ILogger<GmailEmailService> logger,
        IHostEnvironment? hostEnvironment = null)
    {
        _options = options.Value;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email sending disabled; skipping {TemplateKey} email to {To}.", message.TemplateKey, message.To);
            return;
        }

        if (_options.SandboxMode)
        {
            _logger.LogInformation(
                "Sandbox mode: would send {TemplateKey} email to {To} — Subject: {Subject}",
                message.TemplateKey, message.To, message.Subject);
            return;
        }

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            if (_options.AllowInvalidCertificateForDevelopment && IsDevelopmentEnvironment())
            {
                // Some local networks/macOS configurations cannot complete the certificate
                // revocation check Gmail presents during STARTTLS. This is opt-in and physically
                // restricted to Development; production continues to reject every invalid or
                // unverifiable certificate.
                client.ServerCertificateValidationCallback = (_, _, _, _) => true;
                _logger.LogWarning(
                    "Development-only SMTP certificate validation bypass is enabled for {Host}. Do not enable this outside local development.",
                    _options.SmtpHost);
            }
            var secureSocketOptions = _options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secureSocketOptions, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never log _options.Password — only recipient/subject context, mirroring
            // CloudinaryFileStorageService's logging pattern for third-party send failures.
            _logger.LogError(ex, "Failed to send {TemplateKey} email to {To}.", message.TemplateKey, message.To);
            throw new ApiException(HttpStatusCode.InternalServerError, ErrorCode.EMAIL_SEND_FAILED, "The email could not be sent.");
        }

        _logger.LogInformation("Sent {TemplateKey} email to {To}.", message.TemplateKey, message.To);
    }

    private bool IsDevelopmentEnvironment() => _hostEnvironment?.IsDevelopment() == true
        || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), Environments.Development, StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), Environments.Development, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task SendAgencyDeclinedAsync(string toEmail, string shipperName, string loadReference, string agencyName, int attemptNo, CancellationToken cancellationToken = default)
    {
        var (subject, htmlBody, textBody) = EmailTemplates.BuildAgencyDeclined(shipperName, loadReference, agencyName, attemptNo);
        return SendAsync(new EmailMessage
        {
            To = toEmail,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            TemplateKey = NotificationCategory.AgencyDeclined,
            Metadata = new Dictionary<string, string>
            {
                ["loadReference"] = loadReference,
                ["agencyName"] = agencyName,
                ["attemptNo"] = attemptNo.ToString()
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendNewMatchFoundAsync(string toEmail, string shipperName, string loadReference, string agencyName, CancellationToken cancellationToken = default)
    {
        var (subject, htmlBody, textBody) = EmailTemplates.BuildNewMatchFound(shipperName, loadReference, agencyName);
        return SendAsync(new EmailMessage
        {
            To = toEmail,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            TemplateKey = NotificationCategory.NewMatchFound,
            Metadata = new Dictionary<string, string>
            {
                ["loadReference"] = loadReference,
                ["agencyName"] = agencyName
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendNoAutomaticMatchFoundAsync(string toEmail, string shipperName, string loadReference, CancellationToken cancellationToken = default)
    {
        var (subject, htmlBody, textBody) = EmailTemplates.BuildNoAutomaticMatchFound(shipperName, loadReference);
        return SendAsync(new EmailMessage
        {
            To = toEmail,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            TemplateKey = NotificationCategory.NoAutomaticMatch,
            Metadata = new Dictionary<string, string>
            {
                ["loadReference"] = loadReference
            }
        }, cancellationToken);
    }
}
