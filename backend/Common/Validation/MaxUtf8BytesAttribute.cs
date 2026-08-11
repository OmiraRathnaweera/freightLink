using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Validates that a string field does not exceed a given UTF-8-encoded byte length. Not the same
/// as <see cref="StringLengthAttribute"/>, which counts UTF-16 chars — a string using multi-byte
/// UTF-8 characters (e.g. emoji, CJK) can exceed a byte limit well under the equivalent char count,
/// so a char-based length check alone isn't sufficient wherever an exact byte budget matters (e.g.
/// <see cref="PasswordPolicy.MaxBytes"/>, BCrypt's hard input limit).
/// </summary>
public class MaxUtf8BytesAttribute : ValidationAttribute
{
    private readonly int _maxBytes;

    /// <summary>Creates the attribute with the maximum allowed UTF-8 byte length.</summary>
    /// <param name="maxBytes">The maximum number of UTF-8-encoded bytes allowed.</param>
    public MaxUtf8BytesAttribute(int maxBytes)
        : base($"This field must not exceed {maxBytes} bytes when UTF-8 encoded.")
    {
        _maxBytes = maxBytes;
    }

    /// <summary>Returns whether <paramref name="value"/> is a string within the configured byte budget.</summary>
    /// <param name="value">The candidate value.</param>
    /// <returns><c>true</c> if <paramref name="value"/> is not a string, or its UTF-8 byte length is within budget — <c>[Required]</c> handles null/empty separately.</returns>
    public override bool IsValid(object? value)
    {
        return value is not string str || Encoding.UTF8.GetByteCount(str) <= _maxBytes;
    }
}
