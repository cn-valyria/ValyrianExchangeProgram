using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Api.Auth;
using Valyria.Core.Contracts;
using Valyria.Core.Interfaces;

namespace Valyria.Api.Controllers;

/// <summary>
/// Endpoints for the Discord bot (discord-bot/), authenticated with its API key rather than a user's JWT
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.DiscordBot)]
public class DiscordController : ControllerBase
{
    private readonly IAccountsRepository _accountsRepository;
    private readonly IDiscordLinkTokenProvider _linkTokenProvider;
    private readonly ILogger<DiscordController> _logger;

    public DiscordController(IAccountsRepository accountsRepository, IDiscordLinkTokenProvider linkTokenProvider,
        ILogger<DiscordController> logger)
    {
        _accountsRepository = accountsRepository;
        _linkTokenProvider = linkTokenProvider;
        _logger = logger;
    }

    /// <summary>
    /// The accounts (with their unique codes) linked to a Discord user ID. Empty if there are none
    /// </summary>
    [HttpGet("users/{discordId:long}/accounts")]
    public async Task<IActionResult> GetAccountsByDiscordId(long discordId)
    {
        try
        {
            var accounts = (await _accountsRepository.GetAccountsByDiscordIdAsync(discordId)).ToList();

            _logger.LogInformation("{Method} found {Count} accounts", nameof(GetAccountsByDiscordId), accounts.Count);

            return Ok(accounts);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountsByDiscordId)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// A short-lived token for the /register link, for the Discord user who ran the command. The member opens the
    /// link while logged into VEP and approves it, which links this Discord account to their VEP account
    /// (see DiscordLinkController)
    /// </summary>
    [HttpPost("link-tokens")]
    public IActionResult CreateLinkToken([FromBody] DiscordLinkTokenRequest request)
    {
        if (!long.TryParse(request.DiscordId, out var discordId) || discordId <= 0)
            return BadRequest(new { error = "DiscordId must be a Discord user ID" });

        var (token, expiresAt) = _linkTokenProvider.CreateToken(discordId, request.DiscordUsername.Trim());

        _logger.LogInformation("{Method} issued a link token for Discord user {DiscordId}", nameof(CreateLinkToken), discordId);

        return Ok(new DiscordLinkTokenResponse { Token = token, ExpiresAt = expiresAt });
    }
}
