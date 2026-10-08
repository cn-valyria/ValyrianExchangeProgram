using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Core.DTOs;
using Valyria.Core.Interfaces;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SlotsController : ControllerBase
{
    private readonly IDealsRepository _dealsRepository;
    private readonly ILogger<SlotsController> _logger;

    public SlotsController(
        IDealsRepository dealsRepository,
        ILogger<SlotsController> logger)
    {
        _dealsRepository = dealsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all slots for a specific account
    /// </summary>
    [HttpGet("account/{accountId}")]
    public async Task<IActionResult> GetAccountSlots(int accountId)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with accountId={AccountId}", nameof(GetAccountSlots), accountId);

        try
        {
            var result = await _dealsRepository.GetAccountSlotsAsync(accountId);

            if (result == null)
            {
                _logger.LogWarning("Account with id={AccountId} not found", accountId);
                return NotFound(new { error = $"Account with id {accountId} not found" });
            }

            _logger.LogInformation("{MethodName} execution complete. Found {SlotCount} slots",
                nameof(GetAccountSlots), result.Slots.Count);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountSlots)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Update the cycle values of a slot
    /// </summary>
    [HttpPut("{slotId}")]
    public async Task<IActionResult> UpdateSlot(int slotId, [FromBody] SlotUpdateRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with slotId={SlotId}", nameof(UpdateSlot), slotId);

        try
        {
            var username = User.Identity?.Name ?? "unknown";
            request.SlotId = slotId;

            var updated = await _dealsRepository.UpdateSlotAsync(request, username);

            if (!updated)
            {
                _logger.LogWarning("Slot with id={SlotId} not found", slotId);
                return NotFound(new { error = $"Slot with id {slotId} not found" });
            }

            _logger.LogInformation("{MethodName} execution complete for slotId={SlotId}", nameof(UpdateSlot), slotId);

            return NoContent();
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(UpdateSlot)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Get all slots for a specific account that are not part of an active deal
    /// </summary>
    [HttpGet("available/{accountId}")]
    public async Task<IActionResult> GetAvailableSlots(int accountId)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with accountId={AccountId}", nameof(GetAvailableSlots), accountId);

        try
        {
            var result = await _dealsRepository.GetAvailableSlotsAsync(accountId);

            _logger.LogInformation("{MethodName} execution complete. Found {SlotCount} available slots",
                nameof(GetAvailableSlots), result.Count);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAvailableSlots)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }
}
