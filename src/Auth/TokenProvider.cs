using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using api.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace api.Auth;

public class TokenProvider : ITokenProvider
{
    private readonly string _jwtSecret;
    private readonly ILogger<TokenProvider> _logger;

    public TokenProvider(IConfiguration configuration, ILogger<TokenProvider> logger)
    {
        _jwtSecret = configuration["JwtSecret"];
        _logger = logger;
    }

    public string GenerateAccessToken(AuthorizeUserRequest account) => GenerateToken(account, 1);

    public string GenerateRefreshToken(AuthorizeUserRequest account) => GenerateToken(account, 30);

    private string GenerateToken(AuthorizeUserRequest account, int daysExpiresIn)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSecret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(TokenClaims.NationId, account.NationId ?? string.Empty),
                new Claim(TokenClaims.RulerName, account.RulerName ?? string.Empty),
                new Claim(TokenClaims.UniqueCode, account.UniqueCode)
            }),
            Expires = DateTime.UtcNow.AddDays(daysExpiresIn),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public AuthorizeUserRequest ReadToken(string token)
    {
        if (token is null)
            return null;

        if (!ValidateToken(token))
            return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(token);
        return new AuthorizeUserRequest
        {
            NationId = jwt.Claims.First(c => c.Type == TokenClaims.NationId).Value,
            RulerName = jwt.Claims.First(c => c.Type == TokenClaims.RulerName).Value,
            UniqueCode = jwt.Claims.First(c => c.Type == TokenClaims.UniqueCode).Value
        };
    }

    public bool ValidateToken(string token)
    {
        _logger.LogDebug($"Validate token: {token}, against secret: {_jwtSecret}");

        if (token is null)
            return false;
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSecret);

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            _logger.LogDebug($"Validated token: {JsonSerializer.Serialize(validatedToken)}");

            // This technically should only make it this far if ValidateToken worked, but it's a nice 
            // way to sanity check the boolean result
            return validatedToken.ValidTo > DateTime.UtcNow;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while executing tokenHandler.ValidateToken");
            return false;
        }
    }
}

public interface ITokenProvider
{
    string GenerateAccessToken(AuthorizeUserRequest account);
    string GenerateRefreshToken(AuthorizeUserRequest account);
    AuthorizeUserRequest ReadToken(string token);
    bool ValidateToken(string token);
}

public static class TokenClaims
{
    public const string NationId = "nid";
    public const string RulerName = "rul";
    public const string UniqueCode = "uqc";
}