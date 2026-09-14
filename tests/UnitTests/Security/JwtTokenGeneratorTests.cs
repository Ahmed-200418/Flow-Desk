using System.IdentityModel.Tokens.Jwt;
using FlowDesk.Domain.Entities;
using FlowDesk.Infrastructure.Options;
using FlowDesk.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace FlowDesk.UnitTests.Security;

public class JwtTokenGeneratorTests
{
    private readonly JwtTokenGenerator _jwtTokenGenerator;

    public JwtTokenGeneratorTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Secret = "SuperSecretKeyForFlowDeskJWTTokenGeneration2026!MustBeVeryLong32Chars",
            Issuer = "FlowDesk.API",
            Audience = "FlowDesk.Clients",
            ExpiryMinutes = 15
        });

        _jwtTokenGenerator = new JwtTokenGenerator(options);
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainUserClaimsAndPermissions()
    {
        var user = new User("user@flowdesk.local", "hashed_pwd", "John", "Doe");
        var roles = new[] { "Employee" };
        var permissions = new[] { "Purchase.Create", "Purchase.Read" };

        var (token, expiresAtUtc) = _jwtTokenGenerator.GenerateAccessToken(user, roles, permissions);

        Assert.NotNull(token);
        Assert.True(expiresAtUtc > DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Equal(user.Email, jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Contains(jwtToken.Claims, c => c.Type == "permission" && c.Value == "Purchase.Create");
        Assert.Contains(jwtToken.Claims, c => c.Type == "permission" && c.Value == "Purchase.Read");
    }

    [Fact]
    public void GenerateRefreshToken_ShouldBeUniqueString()
    {
        var token1 = _jwtTokenGenerator.GenerateRefreshToken();
        var token2 = _jwtTokenGenerator.GenerateRefreshToken();

        Assert.NotNull(token1);
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void HashToken_ShouldReturnConsistentHash()
    {
        var token = "SampleRawTokenString";
        var hash1 = _jwtTokenGenerator.HashToken(token);
        var hash2 = _jwtTokenGenerator.HashToken(token);

        Assert.Equal(hash1, hash2);
    }
}
