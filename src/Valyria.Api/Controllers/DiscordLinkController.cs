using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Api.Auth;
using Valyria.Core.Contracts;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Exceptions;

namespace Valyria.Api.Controllers;

/// <summary>
/// Members linking their Discord account to their own VEP account, from the Discord bot's /register link. The link
/// carries a token the bot got from DiscordController, so the Discord ID can't be swapped for someone else's, and it's
/// only ever linked to the account the member is logged into
/// </summary>
[ApiController]
[Route("api/discord-link")]
[Authorize]
public class DiscordLinkController : ControllerBase
{
    private const string InvalidTokenMessage = "This link is invalid or has expired. Run /register in Discord to get a new one.";

    private readonly IAccountsRepository _accountsRepository;
    private readonly IDiscordLinkTokenProvider _linkTokenProvider;
    private readonly ILogger<DiscordLinkController> _logger;

    public DiscordLinkController(IAccountsRepository accountsRepository, IDiscordLinkTokenProvider linkTokenProvider,
        ILogger<DiscordLinkController> logger)
    {
        _accountsRepository = accountsRepository;
        _linkTokenProvider = linkTokenProvider;
        _logger = logger;
    }

    /// <summary>
    /// The Discord account a link token is for, so the member can check it before approving
    /// </summary>
    [HttpPost("preview")]
    public IActionResult Preview([FromBody] DiscordLinkRequest request)
    {
        var link = _linkTokenProvider.ReadToken(request.Token);
        if (link is null)
            return BadRequest(new { error = InvalidTokenMessage });

        return Ok(ToDetails(link));
    }

    /// <summary>
    /// Links the token's Discord account to the logged-in member's account, replacing any Discord account it had
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Link([FromBody] DiscordLinkRequest request)
    {
        if (User.GetAccountId() is not { } accountId)
            return Forbid();

        var link = _linkTokenProvider.ReadToken(request.Token);
        if (link is null)
            return BadRequest(new { error = InvalidTokenMessage });

        try
        {
            await _accountsRepository.LinkDiscordAccountAsync(accountId, link.DiscordId, link.DiscordUsername);

            _logger.LogInformation("{Method} linked Discord user {DiscordId} to account {AccountId}",
                nameof(Link), link.DiscordId, accountId);

            return Ok(ToDetails(link));
        }
        catch (NotFoundException e)
        {
            _logger.LogWarning(e.Message);
            return NotFound(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(Link)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    private static DiscordLinkDetails ToDetails(DiscordLink link) => new()
    {
        DiscordId = link.DiscordId.ToString(),
        DiscordUsername = link.DiscordUsername,
        ExpiresAt = link.ExpiresAt
    };
}
