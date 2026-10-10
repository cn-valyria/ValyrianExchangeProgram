using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Valyria.Api.Auth;
using Valyria.Core.Contracts;
using Valyria.Core.DTOs;

namespace Valyria.UnitTests.Api.Auth;

public class TokenProviderTests
{
    internal const string Secret = "unit-test-secret-that-is-long-enough-for-hmac-sha256-signing!!";

    /// <summary>
    /// Keeps every formatted log message, so tests can check nothing sensitive is written
    /// </summary>
    private sealed class RecordingLogger : ILogger<TokenProvider>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + exception);
    }

    private readonly RecordingLogger _logger = new();

    private TokenProvider CreateProvider(string secret = Secret) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSecret"] = secret }).Build(), _logger);

    internal static readonly AuthorizeUserRequest Credentials = new() { NationId = "123", UniqueCode = "ABCDE" };

    internal static readonly AuthorizedAccount Account = new()
    {
        AccountId = 42,
        NationId = 123,
        RulerName = "Bob",
        Role = 'B',
        Roles = [new AccountRole { Id = 1, Name = AdminRoles.SuperAdmin }, new AccountRole { Id = 3, Name = AdminRoles.Lists }]
    };

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static string Claim(JwtSecurityToken token, string type) => token.Claims.Single(c => c.Type == type).Value;

    [Fact]
    public void GenerateAccessToken_ExpiresInOneDay()
    {
        var token = Read(CreateProvider().GenerateAccessToken(Credentials, Account));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddDays(1).AddMinutes(-1), DateTime.UtcNow.AddDays(1).AddMinutes(1));
    }

    [Fact]
    public void GenerateRefreshToken_ExpiresInThirtyDays()
    {
        var token = Read(CreateProvider().GenerateRefreshToken(Credentials, Account));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddDays(30).AddMinutes(-1), DateTime.UtcNow.AddDays(30).AddMinutes(1));
    }

    [Fact]
    public void GeneratedTokens_CarryTheAccountsNameRoleAndAdminRoles()
    {
        var token = Read(CreateProvider().GenerateAccessToken(Credentials, Account));

        Assert.Equal("42", Claim(token, TokenClaims.AccountId));
        Assert.Equal("Bob", Claim(token, TokenClaims.Name));
        Assert.Equal("B", Claim(token, TokenClaims.VepRole));
        Assert.Equal([AdminRoles.SuperAdmin, AdminRoles.Lists], token.Claims.Where(c => c.Type == TokenClaims.AdminRole).Select(c => c.Value));
    }

    [Fact]
    public void GeneratedTokens_FillInNationIdAndRulerName_FromTheAccount()
    {
        // Logging in by ruler name alone still gets both credentials in the token
        var token = Read(CreateProvider().GenerateAccessToken(new AuthorizeUserRequest { RulerName = "Bob", UniqueCode = "ABCDE" }, Account));

        Assert.Equal("123", Claim(token, TokenClaims.NationId));
        Assert.Equal("Bob", Claim(token, TokenClaims.RulerName));
        Assert.Equal("ABCDE", Claim(token, TokenClaims.UniqueCode));
    }

    [Fact]
    public void GeneratedTokens_LeaveOutAdminRoles_ForMembers()
    {
        var token = Read(CreateProvider().GenerateAccessToken(Credentials, new AuthorizedAccount { AccountId = 7, NationId = 1, RulerName = "Sue", Role = 'S', Roles = [] }));

        Assert.DoesNotContain(token.Claims, c => c.Type == TokenClaims.AdminRole);
        Assert.Equal("Sue", Claim(token, TokenClaims.Name));
    }

    [Fact]
    public void ReadToken_RoundTripsTheCredentials()
    {
        var provider = CreateProvider();

        var read = provider.ReadToken(provider.GenerateAccessToken(Credentials, Account));

        Assert.Equal("123", read.NationId);
        Assert.Equal("Bob", read.RulerName);
        Assert.Equal("ABCDE", read.UniqueCode);
    }

    [Fact]
    public void ReadToken_ReturnsNull_ForNullToken()
    {
        Assert.Null(CreateProvider().ReadToken(null!));
    }

    [Fact]
    public void ReadToken_ReturnsNull_ForTokenSignedWithAnotherSecret()
    {
        var otherToken = CreateProvider("a-completely-different-secret-that-is-also-long-enough-for-hmac").GenerateAccessToken(Credentials, Account);

        Assert.Null(CreateProvider().ReadToken(otherToken));
    }

    [Fact]
    public void ValidateToken_ReturnsTrue_ForFreshToken()
    {
        var provider = CreateProvider();

        Assert.True(provider.ValidateToken(provider.GenerateAccessToken(Credentials, Account)));
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

    [Fact]
    public void NeverLogsTheSigningSecretOrTheToken()
    {
        var provider = CreateProvider();
        var token = provider.GenerateAccessToken(Credentials, Account);
        var tampered = token[..^4] + "AAAA";

        provider.ValidateToken(token);
        provider.ReadToken(token);
        provider.ValidateToken(tampered);
        provider.ValidateToken("not-a-jwt");

        Assert.NotEmpty(_logger.Messages);
        Assert.All(_logger.Messages, message =>
        {
            Assert.DoesNotContain(Secret, message);
            Assert.DoesNotContain(token, message);
            Assert.DoesNotContain(tampered, message);
        });
    }
}
