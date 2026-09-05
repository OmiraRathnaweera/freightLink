using FreightLink.Api.Common.Options;
using FreightLink.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="GmailEmailService"/>. Covers only the paths that never touch the
/// network — disabled mode, sandbox mode, and <see cref="EmailOptions.Validate"/> — mirroring how
/// <c>CloudinaryFileStorageService</c>'s real API calls aren't unit-tested either (see
/// <c>FileUploadServiceTests</c>'s doc comment: this project has no mocking framework, and no service
/// test here fakes a third-party SDK/transport, only the service's own interface one level up).
/// </summary>
public class GmailEmailServiceTests
{
    /// <summary>Hand-rolled capturing <see cref="ILogger{T}"/> — no mocking framework in this project.</summary>
    private class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    private static GmailEmailService CreateSut(EmailOptions options, out CapturingLogger<GmailEmailService> logger)
    {
        logger = new CapturingLogger<GmailEmailService>();
        return new GmailEmailService(Options.Create(options), logger);
    }

    [Fact]
    public async Task SendAsync_Disabled_NoOps_WithNoConfigRequired()
    {
        var sut = CreateSut(new EmailOptions { Enabled = false }, out var logger);

        await sut.SendNoAutomaticMatchFoundAsync("shipper@example.com", "Kasun", "REF-100");

        Assert.Contains(logger.Messages, m => m.Contains("disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SendAsync_SandboxMode_NoOps_WithNoRealConfigRequired()
    {
        var sut = CreateSut(new EmailOptions { Enabled = true, SandboxMode = true }, out var logger);

        await sut.SendNoAutomaticMatchFoundAsync("shipper@example.com", "Kasun", "REF-101");

        Assert.Contains(logger.Messages, m => m.Contains("Sandbox", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SendAgencyDeclinedAsync_SandboxMode_LogsCorrectRecipientAndSubject()
    {
        var sut = CreateSut(new EmailOptions { Enabled = true, SandboxMode = true }, out var logger);

        await sut.SendAgencyDeclinedAsync("shipper@example.com", "Kasun", "REF-102", "Swift Cargo", 1);

        Assert.Contains(logger.Messages, m => m.Contains("shipper@example.com") && m.Contains("REF-102"));
    }

    [Fact]
    public async Task SendNewMatchFoundAsync_SandboxMode_LogsCorrectRecipientAndSubject()
    {
        var sut = CreateSut(new EmailOptions { Enabled = true, SandboxMode = true }, out var logger);

        await sut.SendNewMatchFoundAsync("shipper@example.com", "Kasun", "REF-103", "Northline Logistics");

        Assert.Contains(logger.Messages, m => m.Contains("shipper@example.com") && m.Contains("REF-103"));
    }

    [Theory]
    [InlineData("SmtpHost")]
    [InlineData("SmtpPort")]
    [InlineData("Username")]
    [InlineData("Password")]
    [InlineData("FromAddress")]
    public void Validate_Throws_WhenEnabledAndOneRequiredFieldMissing(string missingField)
    {
        var options = ValidEmailOptions();
        typeof(EmailOptions).GetProperty(missingField)!.SetValue(options,
            missingField == "SmtpPort" ? 0 : string.Empty);

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Validate_DoesNotThrow_WhenEnabledAndAllRequiredFieldsPresent()
    {
        var options = ValidEmailOptions();

        var exception = Record.Exception(() => options.Validate());

        Assert.Null(exception);
    }

    [Fact]
    public async Task Validate_IsNotCalled_OrIsHarmless_WhenDisabledRegardlessOfMissingFields()
    {
        // EmailOptions.Validate() only checks field presence — Program.cs is what gates the call on
        // Enabled. This test documents that a disabled-but-empty options object is a valid, supported
        // state for the service itself (SendAsync short-circuits before ever needing these fields).
        var options = new EmailOptions { Enabled = false };
        var sut = CreateSut(options, out _);

        var exception = await Record.ExceptionAsync(() => sut.SendNoAutomaticMatchFoundAsync("shipper@example.com", "Kasun", "REF-104"));

        Assert.Null(exception);
    }

    private static EmailOptions ValidEmailOptions() => new()
    {
        Enabled = true,
        SmtpHost = "smtp.gmail.com",
        SmtpPort = 587,
        EnableSsl = true,
        Username = "sender@example.com",
        Password = "app-password",
        FromName = "FreightLink",
        FromAddress = "sender@example.com"
    };
}
