using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using api.Auth;
using api.Contracts;
using api.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace api;

public class UserEntryPoint
{
    private readonly ITokenProvider _tokenProvider;
    private readonly IAuthenticationProvider _authenticationProvider;
    private readonly ILogger<UserEntryPoint> _logger;

    public UserEntryPoint(
        ITokenProvider tokenProvider,
        IAuthenticationProvider authenticationProvider,
        ILogger<UserEntryPoint> logger)
    {
        _tokenProvider = tokenProvider;
        _authenticationProvider = authenticationProvider;
        _logger = logger;
    }

    [Function(nameof(Authenticate))]
    public async Task<IActionResult> Authenticate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user/authenticate")] HttpRequest request)
    {
        _logger.LogInformation($"Beginning execution for {nameof(Authenticate)} method...");

        var account = await JsonSerializer.DeserializeAsync<AuthorizeUserRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (string.IsNullOrEmpty(account.UniqueCode))
            return new BadRequestObjectResult("Must provide a uniqueCode parameter in the request");

        if (string.IsNullOrEmpty(account.NationId) && string.IsNullOrEmpty(account.RulerName))
            return new BadRequestObjectResult("Must provide either a nationId or rulerName parameter in the request");

        try
        {
            if (!await _authenticationProvider.IsAuthenticated(account))
                return new UnauthorizedResult();

            _logger.LogInformation($"Account (NationId: {account.NationId}, RulerName: {account.RulerName}, UniqueCode: {account.UniqueCode}) was authenticated");

            return new OkObjectResult(GenerateTokenResponse(account));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(Authenticate)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(RefreshToken))]
    public async Task<IActionResult> RefreshToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user/refresh")] HttpRequest request)
    {
        _logger.LogInformation($"Beginning execution for {nameof(RefreshToken)} method...");

        try
        {
            var body = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(request.Body);
            if (!body.TryGetValue("refresh_token", out var refreshToken))
                return new UnauthorizedResult();

            var account = _tokenProvider.ReadToken(refreshToken);
            if (account is null)
                return new UnauthorizedResult();

            _logger.LogInformation($"Account (NationId: {account.NationId}, RulerName: {account.RulerName}, UniqueCode: {account.UniqueCode}) was authenticated");

            return new OkObjectResult(GenerateTokenResponse(account));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(RefreshToken)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(GetAuthorizedAccount)), FunctionAuthorize]
    public async Task<IActionResult> GetAuthorizedAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user/account")] HttpRequest request)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAuthorizedAccount)} method...");

        try 
        {
            var originalAuthRequest = _tokenProvider.ReadToken(request.GetJwtBearerToken());
            if (originalAuthRequest is null)
                return new UnauthorizedResult();

            var authorizedAccount = await _authenticationProvider.GetAuthorizedAccount(originalAuthRequest);

            _logger.LogInformation($"Account (Id: {authorizedAccount.AccountId}, Roles: {string.Join(',', authorizedAccount.Roles)}) was fetched");

            return new OkObjectResult(authorizedAccount);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAuthorizedAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
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
