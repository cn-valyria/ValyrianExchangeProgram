using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Valyria.Api.Auth;
using Valyria.Core.Contracts;
using Valyria.Core.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly ITokenProvider _tokenProvider;
    private readonly IAuthenticationProvider _authenticationProvider;
    private readonly ILogger<UserController> _logger;

    public UserController(
        ITokenProvider tokenProvider,
        IAuthenticationProvider authenticationProvider,
        ILogger<UserController> logger)
    {
        _tokenProvider = tokenProvider;
        _authenticationProvider = authenticationProvider;
        _logger = logger;
    }

    /// <summary>
    /// OAuth 2.0 style token endpoint supporting password and refresh_token grant types
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] TokenRequest request)
    {
        _logger.LogInformation("Token endpoint called with grant_type: {GrantType}", request.GrantType);

        try
        {
            return request.GrantType?.ToLower() switch
            {
                "password" => await HandlePasswordGrant(request),
                "refresh_token" => await HandleRefreshTokenGrant(request),
                _ => BadRequest(new { error = "unsupported_grant_type", error_description = "The grant_type must be 'password' or 'refresh_token'" })
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error occurred while processing token request");
            return StatusCode(500, new { error = "server_error", error_description = e.Message });
        }
    }

    /// <summary>
    /// Legacy endpoint for backward compatibility - redirects to OAuth token endpoint
    /// </summary>
    [HttpPost("authenticate")]
    public async Task<IActionResult> Authenticate([FromBody] AuthorizeUserRequest account)
    {
        _logger.LogInformation("Legacy authenticate endpoint called, redirecting to token endpoint");

        var tokenRequest = new TokenRequest
        {
            GrantType = "password",
            NationId = account.NationId,
            RulerName = account.RulerName,
            Password = account.UniqueCode
        };

        return await Token(tokenRequest);
    }

    /// <summary>
    /// Legacy refresh endpoint for backward compatibility
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] Dictionary<string, string> body)
    {
        _logger.LogInformation("Legacy refresh endpoint called, redirecting to token endpoint");

        if (!body.TryGetValue("refresh_token", out var refreshToken))
            return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required" });

        var tokenRequest = new TokenRequest
        {
            GrantType = "refresh_token",
            RefreshToken = refreshToken
        };

        return await Token(tokenRequest);
    }

    private async Task<IActionResult> HandlePasswordGrant(TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.Password))
            return BadRequest(new { error = "invalid_request", error_description = "password is required" });

        if (string.IsNullOrEmpty(request.NationId) && string.IsNullOrEmpty(request.RulerName))
            return BadRequest(new { error = "invalid_request", error_description = "Either nation_id or ruler_name is required" });

        var authRequest = new AuthorizeUserRequest
        {
            NationId = request.NationId,
            RulerName = request.RulerName,
            UniqueCode = request.Password
        };

        var account = await _authenticationProvider.GetAuthorizedAccount(authRequest);
        if (account is null)
        {
            _logger.LogWarning("Authentication failed for NationId: {NationId}, RulerName: {RulerName}",
                request.NationId, request.RulerName);
            return Unauthorized(new { error = "invalid_grant", error_description = "Invalid credentials" });
        }

        _logger.LogInformation("User authenticated successfully. NationId: {NationId}, RulerName: {RulerName}",
            request.NationId, request.RulerName);

        return Ok(GenerateTokenResponse(authRequest, account));
    }

    private async Task<IActionResult> HandleRefreshTokenGrant(TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required" });

        var credentials = _tokenProvider.ReadToken(request.RefreshToken);
        if (credentials is null)
        {
            _logger.LogWarning("Invalid or expired refresh token");
            return Unauthorized(new { error = "invalid_grant", error_description = "Invalid or expired refresh token" });
        }

        // Validate that the user still exists, and pick up any changes to their roles
        var account = await _authenticationProvider.GetAuthorizedAccount(credentials);
        if (account is null)
        {
            _logger.LogWarning("User no longer exists. NationId: {NationId}, RulerName: {RulerName}",
                credentials.NationId, credentials.RulerName);
            return Unauthorized(new { error = "invalid_grant", error_description = "User account no longer valid" });
        }

        _logger.LogInformation("Refresh token validated. NationId: {NationId}, RulerName: {RulerName}",
            credentials.NationId, credentials.RulerName);

        return Ok(GenerateTokenResponse(credentials, account));
    }

    /// <summary>
    /// Get the authenticated user's account information
    /// </summary>
    [HttpGet("account")]
    [Authorize]
    public async Task<IActionResult> GetAuthorizedAccount()
    {
        _logger.LogInformation("GetAuthorizedAccount endpoint called");

        try
        {
            // Get the AccountId from claims (refreshed by CustomJwtBearerEvents)
            if (User.GetAccountId() is null)
            {
                _logger.LogWarning("AccountId claim not found or invalid in authenticated user");

                // Fallback: read from token manually
                var authHeader = Request.Headers["Authorization"].ToString();
                var token = authHeader.StartsWith("Bearer ") ? authHeader.Substring(7) : authHeader;

                var originalAuthRequest = _tokenProvider.ReadToken(token);
                if (originalAuthRequest is null)
                    return Unauthorized(new { error = "invalid_token", error_description = "Token is invalid" });

                var authorizedAccount = await _authenticationProvider.GetAuthorizedAccount(originalAuthRequest);
                if (authorizedAccount == null)
                    return Unauthorized(new { error = "invalid_token", error_description = "User not found" });

                _logger.LogInformation("Account fetched (fallback). AccountId: {AccountId}", authorizedAccount.AccountId);
                return Ok(authorizedAccount);
            }

            // Reconstruct the auth request from claims
            var nationIdClaim = User.FindFirst(TokenClaims.NationId)?.Value;
            var rulerNameClaim = User.FindFirst(TokenClaims.RulerName)?.Value;
            var uniqueCodeClaim = User.FindFirst(TokenClaims.UniqueCode)?.Value;

            if (string.IsNullOrEmpty(uniqueCodeClaim))
                return Unauthorized(new { error = "invalid_token", error_description = "Token missing required claims" });

            var authRequest = new AuthorizeUserRequest
            {
                NationId = nationIdClaim,
                RulerName = rulerNameClaim,
                UniqueCode = uniqueCodeClaim
            };

            var account = await _authenticationProvider.GetAuthorizedAccount(authRequest);
            if (account == null)
                return Unauthorized(new { error = "invalid_token", error_description = "User not found" });

            _logger.LogInformation("Account fetched. AccountId: {AccountId}", account.AccountId);
            return Ok(account);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while fetching authorized account");
            return StatusCode(500, new { error = "server_error", error_description = e.Message });
        }
    }

    private TokenResponse GenerateTokenResponse(AuthorizeUserRequest credentials, AuthorizedAccount account)
    {
        var accessToken = _tokenProvider.GenerateAccessToken(credentials, account);
        var refreshToken = _tokenProvider.GenerateRefreshToken(credentials, account);

        return new TokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = 86400, // 1 day in seconds
            RefreshToken = refreshToken
        };
    }
}
