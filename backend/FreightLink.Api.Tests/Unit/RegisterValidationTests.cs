using System.ComponentModel.DataAnnotations;
using FreightLink.Api.DTOs.Auth;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>
/// DataAnnotations validation tests for the register DTOs — email format, strong-password rules,
/// required fields, and lat/lng range checks — run directly via <see cref="Validator"/>, without
/// needing HTTP or a service layer.
/// </summary>
public class RegisterValidationTests
{
    /// <summary>Runs full DataAnnotations validation against a DTO instance.</summary>
    private static bool TryValidate(object dto, out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();
        var context = new ValidationContext(dto);
        return Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
    }

    /// <summary>A fully populated, well-formed shipper request passes validation.</summary>
    [Fact]
    public void RegisterShipperRequestDto_IsValid_WithGoodData()
    {
        var dto = new RegisterShipperRequestDto
        {
            Email = "shipper@example.com",
            Password = "Sup3r$ecret1",
            FullName = "Jane Shipper",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        };

        Assert.True(TryValidate(dto, out _));
    }

    /// <summary>A missing or malformed email fails the [EmailAddress]/[Required] rules.</summary>
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void RegisterShipperRequestDto_IsInvalid_ForBadEmail(string email)
    {
        var dto = new RegisterShipperRequestDto
        {
            Email = email,
            Password = "Sup3r$ecret1",
            FullName = "Jane Shipper",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        };

        Assert.False(TryValidate(dto, out _));
    }

    /// <summary>Passwords missing length, case, digit, or special-character requirements fail [StrongPassword].</summary>
    [Theory]
    [InlineData("short1!")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSpecialChars1")]
    public void RegisterShipperRequestDto_IsInvalid_ForWeakPassword(string password)
    {
        var dto = new RegisterShipperRequestDto
        {
            Email = "shipper@example.com",
            Password = password,
            FullName = "Jane Shipper",
            CompanyName = "Acme Freight",
            BillingAddress = "123 Main Street, Colombo"
        };

        Assert.False(TryValidate(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterShipperRequestDto.Password)));
    }

    /// <summary>A fully populated, well-formed agency request passes validation.</summary>
    [Fact]
    public void RegisterAgencyRequestDto_IsValid_WithGoodData()
    {
        var dto = new RegisterAgencyRequestDto
        {
            Email = "agency@example.com",
            Password = "Sup3r$ecret1",
            FullName = "Alex Agency",
            AgencyName = "Speedy Transport",
            BusinessRegNo = "BRN-0001",
            YardAddress = "456 Yard Road, Kandy",
            YardLat = 7.2906m,
            YardLng = 80.6337m
        };

        Assert.True(TryValidate(dto, out _));
    }

    /// <summary>An empty agency request fails validation on every required field, including business reg no and agency name.</summary>
    [Fact]
    public void RegisterAgencyRequestDto_IsInvalid_WhenRequiredFieldsMissing()
    {
        var dto = new RegisterAgencyRequestDto();

        Assert.False(TryValidate(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterAgencyRequestDto.BusinessRegNo)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterAgencyRequestDto.AgencyName)));
    }

    /// <summary>A latitude outside [-90, 90] fails the [Range] rule on YardLat.</summary>
    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void RegisterAgencyRequestDto_IsInvalid_ForOutOfRangeLatitude(decimal lat)
    {
        var dto = new RegisterAgencyRequestDto
        {
            Email = "agency@example.com",
            Password = "Sup3r$ecret1",
            FullName = "Alex Agency",
            AgencyName = "Speedy Transport",
            BusinessRegNo = "BRN-0001",
            YardAddress = "456 Yard Road, Kandy",
            YardLat = lat,
            YardLng = 80.6337m
        };

        Assert.False(TryValidate(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterAgencyRequestDto.YardLat)));
    }

}
