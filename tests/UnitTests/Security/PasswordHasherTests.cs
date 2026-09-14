using FlowDesk.Infrastructure.Services;
using Xunit;

namespace FlowDesk.UnitTests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ShouldReturnSaltAndHashFormat()
    {
        var password = "StrongPassword@123";
        var hash = _hasher.HashPassword(password);

        Assert.NotNull(hash);
        Assert.Contains(".", hash);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        var password = "StrongPassword@123";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(hash, password);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ShouldReturnFalse()
    {
        var password = "StrongPassword@123";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(hash, "WrongPassword@123");

        Assert.False(isValid);
    }
}
