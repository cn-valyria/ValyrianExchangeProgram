using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;

namespace Valyria.Api.Controllers;

/// <summary>
/// Daily CN mass messages for accounts that need to act on their deals. Replaces VEP 2.5's lists,
/// and is what the "Admin - Lists" role is for
/// </summary>
[ApiController]
[Route("api/mass-messages")]
[Authorize]
public class MassMessagesController : ControllerBase
{
    private readonly IMassMessagesRepository _massMessagesRepository;
    private readonly ILogger<MassMessagesController> _logger;

    public MassMessagesController(IMassMessagesRepository massMessagesRepository, ILogger<MassMessagesController> logger)
    {
        _massMessagesRepository = massMessagesRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get every mass message with today's recipients
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMassMessages()
    {
        _logger.LogInformation("Beginning execution for {MethodName}", nameof(GetMassMessages));

        try
        {
            var result = await _massMessagesRepository.GetMassMessagesAsync();

            _logger.LogInformation("{MethodName} execution complete. Found {Count} messages",
                nameof(GetMassMessages), result.Messages.Count);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetMassMessages)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Mark a message as sent (or not sent) for today, so other admins know whether to send it
    /// </summary>
    [HttpPut("{id}/sent")]
    public async Task<IActionResult> SetMessageSent(int id, [FromBody] MassMessageSentRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with messageId={MessageId}, sent={Sent}",
            nameof(SetMessageSent), id, request.Sent);

        try
        {
            if (!int.TryParse(User.FindFirst("AccountId")?.Value, out var accountId))
                return Unauthorized();

            if (!await _massMessagesRepository.SetMessageSentAsync(id, request.Sent, accountId))
            {
                _logger.LogWarning("Mass message with id={MessageId} not found", id);
                return NotFound(new { error = $"Mass message with id {id} not found" });
            }

            _logger.LogInformation("{MethodName} execution complete for messageId={MessageId}", nameof(SetMessageSent), id);

            return NoContent();
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(SetMessageSent)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }
}
