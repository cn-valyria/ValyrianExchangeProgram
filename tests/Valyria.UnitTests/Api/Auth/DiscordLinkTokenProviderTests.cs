using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Valyria.Api.Auth;

namespace Valyria.UnitTests.Api.Auth;

public class DiscordLinkTokenProviderTests
{
    private const long DiscordId = 123456789012345678;

    private static DiscordLinkTokenProvider CreateProvider(string secret = TokenProviderTests.Secret) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSecret"] = secret }).Build(),
            NullLogger<DiscordLinkTokenProvider>.Instance);

    [Fact]
    public void ReadToken_ReturnsTheDiscordUserTheTokenWasIssuedFor()
    {
        var provider = CreateProvider();
        var (token, expiresAt) = provider.CreateToken(DiscordId, "bob_");

        var link = provider.ReadToken(token);

        Assert.NotNull(link);
        Assert.Equal(DiscordId, link.DiscordId);
        Assert.Equal("bob_", link.DiscordUsername);
        Assert.InRange(expiresAt, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(15));
        Assert.Equal(expiresAt, link.ExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public void ReadToken_ReturnsNull_ForAMissingOrMalformedToken(string? token)
    {
        Assert.Null(CreateProvider().ReadToken(token));
    }

    [Fact]
    public void ReadToken_ReturnsNull_ForATokenSignedWithAnotherSecret()
    {
        var (token, _) = CreateProvider("another-secret-that-is-long-enough-for-hmac-sha256-signing!!!").CreateToken(DiscordId, "bob_");

        Assert.Null(CreateProvider().ReadToken(token));
    }

    [Fact]
    public void ReadToken_ReturnsNull_WhenTheDiscordIdIsEdited()
    {
        var provider = CreateProvider();
        var (token, _) = provider.CreateToken(DiscordId, "bob_");
        var parts = token.Split('.');
        var payload = Base64UrlDecode(parts[1]).Replace(DiscordId.ToString(), "999999999999999999");
        var edited = $"{parts[0]}.{Base64UrlEncode(payload)}.{parts[2]}";

        Assert.Null(provider.ReadToken(edited));
    }

    [Fact]
    public void ReadToken_ReturnsNull_ForAnUnsignedToken()
    {
        var unsigned = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            audience: "vep-discord-link",
            claims: [new("did", DiscordId.ToString()), new("dun", "bob_")],
            expires: DateTime.UtcNow.AddMinutes(5)));

        Assert.Null(CreateProvider().ReadToken(unsigned));
    }

    [Fact]
    public void ReadToken_ReturnsNull_ForAnExpiredToken()
    {
        var provider = CreateProvider();
        var handler = new JwtSecurityTokenHandler();
        var (token, _) = provider.CreateToken(DiscordId, "bob_");
        Assert.NotNull(provider.ReadToken(token));

        // Same key and claims, but expired: built by re-signing a copy whose lifetime has passed
        var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Security.Cryptography.HMACSHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(TokenProviderTests.Secret),
                System.Text.Encoding.UTF8.GetBytes("vep-discord-link")));
        var expired = handler.WriteToken(new JwtSecurityToken(
            audience: "vep-discord-link",
            claims: [new("did", DiscordId.ToString()), new("dun", "bob_")],
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(signingKey,
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)));

        Assert.Null(provider.ReadToken(expired));
    }

    [Fact]
    public void ReadToken_ReturnsNull_ForALoginToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSecret"] = TokenProviderTests.Secret })
            .Build();
        var loginToken = new TokenProvider(configuration, NullLogger<TokenProvider>.Instance)
            .GenerateAccessToken(TokenProviderTests.Credentials, TokenProviderTests.Account);

        Assert.Null(CreateProvider().ReadToken(loginToken));
    }

    [Fact]
    public void LinkTokens_DoNotPassAsLoginTokens()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSecret"] = TokenProviderTests.Secret })
            .Build();
        var (token, _) = CreateProvider().CreateToken(DiscordId, "bob_");

        Assert.False(new TokenProvider(configuration, NullLogger<TokenProvider>.Instance).ValidateToken(token));
    }

    private static string Base64UrlDecode(string value) =>
        System.Text.Encoding.UTF8.GetString(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(value));

    private static string Base64UrlEncode(string value) =>
        Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(value);
}
