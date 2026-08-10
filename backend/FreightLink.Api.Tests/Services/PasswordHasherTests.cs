using FreightLink.Api.Services;
using Xunit;

namespace FreightLink.Api.Tests.Services;

/// <summary>Unit tests for <see cref="PasswordHasher"/>'s BCrypt hash/verify behavior.</summary>
public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    /// <summary>A hashed password must never equal the original plain text.</summary>
    [Fact]
    public void Hash_DoesNotReturnPlainText()
    {
        var hash = _hasher.Hash("Sup3r$ecret!");

        Assert.NotEqual("Sup3r$ecret!", hash);
        Assert.NotEmpty(hash);
    }

    /// <summary>Verifying the correct password against its own hash succeeds.</summary>
    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var hash = _hasher.Hash("Sup3r$ecret!");

        Assert.True(_hasher.Verify("Sup3r$ecret!", hash));
    }

    /// <summary>Verifying an incorrect password against a hash fails.</summary>
    [Fact]
    public void Verify_ReturnsFalse_ForIncorrectPassword()
    {
        var hash = _hasher.Hash("Sup3r$ecret!");

        Assert.False(_hasher.Verify("WrongPassword1!", hash));
    }
}
