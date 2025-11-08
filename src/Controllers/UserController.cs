using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using api.Auth;
using api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace api.Controllers;

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

    [HttpPost("authenticate")]
    public async Task<IActionResult> Authenticate([FromBody] AuthorizeUserRequest account)
    {
        _logger.LogInformation($"Beginning execution for {nameof(Authenticate)} method...");

        if (string.IsNullOrEmpty(account.UniqueCode))
            return BadRequest("Must provide a uniqueCode parameter in the request");

        if (string.IsNullOrEmpty(account.NationId) && string.IsNullOrEmpty(account.RulerName))
            return BadRequest("Must provide either a nationId or rulerName parameter in the request");

        try
        {
            if (!await _authenticationProvider.IsAuthenticated(account))
                return Unauthorized();

            _logger.LogInformation($"Account (NationId: {account.NationId}, RulerName: {account.RulerName}, UniqueCode: {account.UniqueCode}) was authenticated");

            return Ok(GenerateTokenResponse(account));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(Authenticate)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] Dictionary<string, string> body)
    {
        _logger.LogInformation($"Beginning execution for {nameof(RefreshToken)} method...");

        try
        {
            if (!body.TryGetValue("refresh_token", out var refreshToken))
                return Unauthorized();

            var account = _tokenProvider.ReadToken(refreshToken);
            if (account is null)
                return Unauthorized();

            _logger.LogInformation($"Account (NationId: {account.NationId}, RulerName: {account.RulerName}, UniqueCode: {account.UniqueCode}) was authenticated");

            return Ok(GenerateTokenResponse(account));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(RefreshToken)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("account")]
    [ServiceFilter(typeof(ApiAuthorizeAttribute))]
    public async Task<IActionResult> GetAuthorizedAccount()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAuthorizedAccount)} method...");

        try
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var token = authHeader.StartsWith("Bearer ") ? authHeader.Substring(7) : authHeader;

            var originalAuthRequest = _tokenProvider.ReadToken(token);
            if (originalAuthRequest is null)
                return Unauthorized();

            var authorizedAccount = await _authenticationProvider.GetAuthorizedAccount(originalAuthRequest);

            _logger.LogInformation($"Account (Id: {authorizedAccount.AccountId}, Roles: {string.Join(',', authorizedAccount.Roles)}) was fetched");

            return Ok(authorizedAccount);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAuthorizedAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    private object GenerateTokenResponse(AuthorizeUserRequest account)
    {
        var accessToken = _tokenProvider.GenerateAccessToken(account);
        var newRefreshToken = _tokenProvider.GenerateRefreshToken(account);
        return new
        {
            access_token = accessToken,
            refresh_token = newRefreshToken
        };
    }
}
