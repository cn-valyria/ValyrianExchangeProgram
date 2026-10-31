using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Api.Auth;
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
    private readonly ILogger<DiscordController> _logger;

    public DiscordController(IAccountsRepository accountsRepository, ILogger<DiscordController> logger)
    {
        _accountsRepository = accountsRepository;
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
}
