using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Valyria.Api.Auth;

/// <summary>
/// The Discord user a link token was issued for
/// </summary>
public record DiscordLink(long DiscordId, string DiscordUsername, DateTime ExpiresAt);

/// <summary>
/// Issues and checks the short-lived tokens behind the Discord bot's /register link. The bot asks for one (with its
/// API key) for the Discord user who ran the command, so the link page can trust the Discord ID and username in it:
/// they can't be edited or made up in the browser.
///
/// They're signed with a key derived from JwtSecret rather than JwtSecret itself, so a link token can never pass as a
/// login token, or the other way around.
/// </summary>
public class DiscordLinkTokenProvider : IDiscordLinkTokenProvider
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private const string Audience = "vep-discord-link";
    private const string DiscordIdClaim = "did";
    private const string DiscordUsernameClaim = "dun";

    private readonly SymmetricSecurityKey _signingKey;
    private readonly ILogger<DiscordLinkTokenProvider> _logger;

    public DiscordLinkTokenProvider(IConfiguration configuration, ILogger<DiscordLinkTokenProvider> logger)
    {
        var jwtSecret = configuration["JwtSecret"] ?? throw new InvalidOperationException("JwtSecret is not configured");
        _signingKey = new SymmetricSecurityKey(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtSecret), Encoding.UTF8.GetBytes(Audience)));
        _logger = logger;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(long discordId, string discordUsername)
    {
        var expiresAt = DateTime.UtcNow.Add(Lifetime);
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new List<Claim>
            {
                new(DiscordIdClaim, discordId.ToString()),
                new(DiscordUsernameClaim, discordUsername)
            }),
            Audience = Audience,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
        });

        return (tokenHandler.WriteToken(token), expiresAt);
    }

    public DiscordLink? ReadToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateIssuer = false,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            if (!long.TryParse(principal.FindFirst(DiscordIdClaim)?.Value, out var discordId) || discordId <= 0)
                return null;

            var discordUsername = principal.FindFirst(DiscordUsernameClaim)?.Value;
            if (string.IsNullOrEmpty(discordUsername))
                return null;

            return new DiscordLink(discordId, discordUsername, validatedToken.ValidTo);
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        {
            _logger.LogInformation("Rejected a Discord link token: {Reason}", e.Message);
            return null;
        }
    }
}

public interface IDiscordLinkTokenProvider
{
    (string Token, DateTime ExpiresAt) CreateToken(long discordId, string discordUsername);

    /// <summary>
    /// Null if the token is missing, expired, or wasn't issued by this API
    /// </summary>
    DiscordLink? ReadToken(string? token);
}
