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
    public void BuildJobProposal_WithActionUrls_IncludesAcceptAndDeclineLinks()
    {
        var (subject, html, text) = EmailTemplates.BuildJobProposal(
            "Swift Cargo", "REF-005", "Garments", 1200m, "Colombo Port", "Kandy Depot", 18500m,
            "https://app.example.com/agency/job-proposals/respond?token=accept-raw",
            "https://app.example.com/agency/job-proposals/respond?token=decline-raw");

        Assert.Contains("REF-005", subject);
        Assert.Contains("token=accept-raw", html);
        Assert.Contains("token=decline-raw", html);
        Assert.Contains("token=accept-raw", text);
        Assert.Contains("token=decline-raw", text);
        Assert.DoesNotContain("Please log in to your FreightLink Agency portal", html);
    }

    [Fact]
    public void BuildJobProposal_WithoutActionUrls_FallsBackToLogInCopy()
    {
        var (_, html, text) = EmailTemplates.BuildJobProposal(
            "Swift Cargo", "REF-006", "Garments", 1200m, "Colombo Port", "Kandy Depot", 18500m,
            acceptUrl: null, declineUrl: null);

        Assert.Contains("Please log in to your FreightLink Agency portal", html);
        Assert.Contains("Please log in to your FreightLink Agency portal", text);
        Assert.DoesNotContain("href", text);
    }

    [Fact]
    public void BuildJobProposal_HtmlEncodesInterpolatedValues()
    {
        var (_, html, _) = EmailTemplates.BuildJobProposal(
            "<script>alert(1)</script>", "REF-007", "Garments", 1200m, "Colombo Port", "Kandy Depot", 18500m,
            acceptUrl: null, declineUrl: null);

        Assert.DoesNotContain("<script>alert(1)</script>", html);
    }

    [Fact]
    public void BuildAgencyDeclined_HtmlEncodesInterpolatedValues()
    {
        var (_, html, _) = EmailTemplates.BuildAgencyDeclined("<script>alert(1)</script>", "REF-004", "Agency & Co", 1);

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("Agency &amp; Co", html);
    }
}
