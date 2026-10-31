using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Valyria.Api.Auth;

/// <summary>
/// Authenticates the Discord bot by the shared API key it sends in the X-Api-Key header.
/// The key is the DiscordBotApiKey setting; while it isn't configured, every key is rejected.
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DiscordBotApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string ConfigurationKey = "DiscordBotApiKey";
    public const string ClientName = "Discord bot";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var providedKey = Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(providedKey))
            return Task.FromResult(AuthenticateResult.NoResult());

        var expectedKey = configuration[ConfigurationKey];
        if (string.IsNullOrEmpty(expectedKey))
        {
            Logger.LogWarning("Rejected an API key because {Setting} is not configured", ConfigurationKey);
            return Task.FromResult(AuthenticateResult.Fail("API key authentication is not configured"));
        }

        // Comparing hashes keeps the comparison constant-time regardless of the keys' lengths
        var matches = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(providedKey)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey)));
        if (!matches)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var identity = new ClaimsIdentity([new Claim(TokenClaims.Name, ClientName)], SchemeName, TokenClaims.Name, TokenClaims.AdminRole);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
