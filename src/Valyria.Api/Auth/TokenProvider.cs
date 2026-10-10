using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Valyria.Core.Contracts;
using Valyria.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Valyria.Api.Auth;

public class TokenProvider : ITokenProvider
{
    private readonly string _jwtSecret;
    private readonly ILogger<TokenProvider> _logger;

    public TokenProvider(IConfiguration configuration, ILogger<TokenProvider> logger)
    {
        _jwtSecret = configuration["JwtSecret"];
        _logger = logger;
    }

    public string GenerateAccessToken(AuthorizeUserRequest credentials, AuthorizedAccount account) =>
        GenerateToken(credentials, account, 1);

    public string GenerateRefreshToken(AuthorizeUserRequest credentials, AuthorizedAccount account) =>
        GenerateToken(credentials, account, 30);

    private string GenerateToken(AuthorizeUserRequest credentials, AuthorizedAccount account, int daysExpiresIn)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSecret);
        var claims = new List<Claim>
        {
            // Taken from the account rather than the login request, which only has one of the two
            new(TokenClaims.NationId, account.NationId.ToString()),
            new(TokenClaims.RulerName, account.RulerName ?? string.Empty),
            new(TokenClaims.UniqueCode, credentials.UniqueCode ?? string.Empty)
        };
        claims.AddRange(TokenClaims.ForAccount(account));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
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

            // This technically should only make it this far if ValidateToken worked, but it's a nice 
            // way to sanity check the boolean result
            return validatedToken.ValidTo > DateTime.UtcNow;
        }
        catch (Exception e)
        {
            // The token itself is never logged: it carries the account's unique code
            _logger.LogWarning("Token validation failed: {Reason}", e.Message);
            return false;
        }
    }
}

public interface ITokenProvider
{
    string GenerateAccessToken(AuthorizeUserRequest credentials, AuthorizedAccount account);
    string GenerateRefreshToken(AuthorizeUserRequest credentials, AuthorizedAccount account);
    AuthorizeUserRequest ReadToken(string token);
    bool ValidateToken(string token);
}
