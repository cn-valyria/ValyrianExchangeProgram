using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Api.Auth;
using Valyria.Core.Interfaces;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaderboardController : ControllerBase
{
    private readonly IAccountsRepository _accountsRepository;
    private readonly ILogger<LeaderboardController> _logger;

    public LeaderboardController(IAccountsRepository accountsRepository, ILogger<LeaderboardController> logger)
    {
        _accountsRepository = accountsRepository;
        _logger = logger;
    }

    /// <summary>
    /// The deal partner leaderboard: accounts ranked by unexcused flags per outgoing leg over the last 90 CN days,
    /// and the signed-in account's own standing
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLeaderboard()
    {
        _logger.LogInformation("Beginning execution for {MethodName}", nameof(GetLeaderboard));

        if (User.GetAccountId() is not { } accountId)
            return Unauthorized();

        try
        {
            var leaderboard = await _accountsRepository.GetLeaderboardAsync(accountId);

            _logger.LogInformation("{MethodName} execution complete. Ranked {Count} accounts.", nameof(GetLeaderboard), leaderboard.RankedAccounts);

            return Ok(leaderboard);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetLeaderboard)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }
}
