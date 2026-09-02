using FreightLink.Api.Common.Email;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="EmailTemplates"/>'s pure content-generation methods — no service, no DI,
/// no I/O involved.
/// </summary>
public class EmailTemplatesTests
{
    [Fact]
    public void BuildAgencyDeclined_IncludesAllSuppliedValues()
    {
        var (subject, html, text) = EmailTemplates.BuildAgencyDeclined("Kasun", "REF-001", "Swift Cargo", 2);

        Assert.Contains("REF-001", subject);
        foreach (var body in new[] { html, text })
        {
            Assert.Contains("Kasun", body);
            Assert.Contains("REF-001", body);
            Assert.Contains("Swift Cargo", body);
            Assert.Contains("2", body);
        }
    }

    [Fact]
    public void BuildNewMatchFound_IncludesAllSuppliedValues()
    {
        var (subject, html, text) = EmailTemplates.BuildNewMatchFound("Kasun", "REF-002", "Northline Logistics");

        Assert.Contains("REF-002", subject);
        foreach (var body in new[] { html, text })
        {
            Assert.Contains("Kasun", body);
            Assert.Contains("REF-002", body);
            Assert.Contains("Northline Logistics", body);
        }
    }

    [Fact]
    public void BuildNoAutomaticMatchFound_IncludesAllSuppliedValues()
    {
        var (subject, html, text) = EmailTemplates.BuildNoAutomaticMatchFound("Kasun", "REF-003");

        Assert.Contains("REF-003", subject);
        foreach (var body in new[] { html, text })
        {
            Assert.Contains("Kasun", body);
            Assert.Contains("REF-003", body);
        }
    }

    [Fact]
    public void BuildAgencyDeclined_HtmlEncodesInterpolatedValues()
    {
        var (_, html, _) = EmailTemplates.BuildAgencyDeclined("<script>alert(1)</script>", "REF-004", "Agency & Co", 1);

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("Agency &amp; Co", html);
    }
}
