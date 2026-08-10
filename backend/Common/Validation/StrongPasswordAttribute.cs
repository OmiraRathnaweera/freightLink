using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Validates that a password field is at least 8 characters and contains an uppercase letter,
/// a lowercase letter, a digit, and a special character. Applied to every <c>Password</c>
/// property on the auth request DTOs.
/// </summary>
public class StrongPasswordAttribute : ValidationAttribute
{
    private static readonly Regex PasswordRegex = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
        RegexOptions.Compiled);

    /// <summary>Creates the attribute with its default validation error message.</summary>
    public StrongPasswordAttribute()
        : base("Password must be at least 8 characters long and include an uppercase letter, a lowercase letter, a digit, and a special character.")
    {
    }

    /// <summary>Returns whether <paramref name="value"/> is a string matching the strong-password pattern.</summary>
    /// <param name="value">The candidate password value.</param>
    /// <returns><c>true</c> if the value is a string satisfying all strength rules.</returns>
    public override bool IsValid(object? value)
    {
        return value is string password && PasswordRegex.IsMatch(password);
    }
}
