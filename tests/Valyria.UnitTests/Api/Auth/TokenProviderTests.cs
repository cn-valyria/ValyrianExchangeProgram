using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Valyria.Api.Auth;
using Valyria.Core.Contracts;

namespace Valyria.UnitTests.Api.Auth;

public class TokenProviderTests
{
    internal const string Secret = "unit-test-secret-that-is-long-enough-for-hmac-sha256-signing!!";

    private static TokenProvider CreateProvider(string secret = Secret) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSecret"] = secret }).Build(),
            NullLogger<TokenProvider>.Instance);

    private static readonly AuthorizeUserRequest Account = new() { NationId = "123", RulerName = "Bob", UniqueCode = "ABCDE" };

    [Fact]
    public void GenerateAccessToken_ExpiresInOneDay_AndCarriesTheAccountClaims()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateProvider().GenerateAccessToken(Account));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddDays(1).AddMinutes(-1), DateTime.UtcNow.AddDays(1).AddMinutes(1));
        Assert.Equal("123", token.Claims.Single(c => c.Type == TokenClaims.NationId).Value);
        Assert.Equal("Bob", token.Claims.Single(c => c.Type == TokenClaims.RulerName).Value);
        Assert.Equal("ABCDE", token.Claims.Single(c => c.Type == TokenClaims.UniqueCode).Value);
    }

    [Fact]
    public void GenerateRefreshToken_ExpiresInThirtyDays()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateProvider().GenerateRefreshToken(Account));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddDays(30).AddMinutes(-1), DateTime.UtcNow.AddDays(30).AddMinutes(1));
    }

    [Fact]
    public void GenerateAccessToken_UsesEmptyClaimsForMissingNationIdAndRulerName()
    {
        var provider = CreateProvider();
        var token = provider.GenerateAccessToken(new AuthorizeUserRequest { UniqueCode = "ABCDE" });

        var read = provider.ReadToken(token);

        Assert.Equal(string.Empty, read.NationId);
        Assert.Equal(string.Empty, read.RulerName);
        Assert.Equal("ABCDE", read.UniqueCode);
    }

    [Fact]
    public void ReadToken_RoundTripsAValidToken()
    {
        var provider = CreateProvider();

        var read = provider.ReadToken(provider.GenerateAccessToken(Account));

        Assert.Equal(Account.NationId, read.NationId);
        Assert.Equal(Account.RulerName, read.RulerName);
        Assert.Equal(Account.UniqueCode, read.UniqueCode);
    }

    [Fact]
    public void ReadToken_ReturnsNull_ForNullToken()
    {
        Assert.Null(CreateProvider().ReadToken(null!));
    }

    [Fact]
    public void ReadToken_ReturnsNull_ForTokenSignedWithAnotherSecret()
    {
        var otherToken = CreateProvider("a-completely-different-secret-that-is-also-long-enough-for-hmac").GenerateAccessToken(Account);

        Assert.Null(CreateProvider().ReadToken(otherToken));
    }

    [Fact]
    public void ValidateToken_ReturnsTrue_ForFreshToken()
    {
        var provider = CreateProvider();

        Assert.True(provider.ValidateToken(provider.GenerateAccessToken(Account)));
    }

    [Fact]
    public void ValidateToken_ReturnsFalse_ForNullToken()
    {
        Assert.False(CreateProvider().ValidateToken(null!));
    }

    [Fact]
    public void ValidateToken_ReturnsFalse_ForGarbage()
    {
        Assert.False(CreateProvider().ValidateToken("not-a-jwt"));
    }

    [Fact]
    public void ValidateToken_ReturnsFalse_ForExpiredToken()
    {
        var handler = new JwtSecurityTokenHandler();
        var expired = handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
        {
            NotBefore = DateTime.UtcNow.AddDays(-2),
            Expires = DateTime.UtcNow.AddDays(-1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Secret)), SecurityAlgorithms.HmacSha256Signature)
        }));

        Assert.False(CreateProvider().ValidateToken(expired));
    }
}
