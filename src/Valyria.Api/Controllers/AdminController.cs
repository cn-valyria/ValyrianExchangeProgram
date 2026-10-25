using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Core.Interfaces;
using Valyria.Api.Auth;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.ViewAdminPortal)]
public class AdminController : ControllerBase
{
    private readonly IAdminRepository _adminRepository;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminRepository adminRepository, ILogger<AdminController> logger)
    {
        _adminRepository = adminRepository;
        _logger = logger;
    }

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> GetProgramSummary()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetProgramSummary)} method...");

        try
        {
            var results = await _adminRepository.GetProgramSummaryAsync();

            _logger.LogInformation("GetProgramSummary execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetProgramSummary)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/slot-configs")]
    public async Task<IActionResult> GetSlotConfigs()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetSlotConfigs)} method...");

        try
        {
            var results = await _adminRepository.GetSlotConfigsAsync();

            _logger.LogInformation("GetSlotConfigs execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetSlotConfigs)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/deal-health")]
    public async Task<IActionResult> GetDealHealth()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetDealHealth)} method...");

        try
        {
            var results = await _adminRepository.GetDealHealthAsync();

            _logger.LogInformation("GetDealHealth execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetDealHealth)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/alliance-slot-configs")]
    public async Task<IActionResult> GetAllianceSlotConfigs()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAllianceSlotConfigs)} method...");

        try
        {
            var results = await _adminRepository.GetAllianceSlotConfigsAsync();

            _logger.LogInformation("GetAllianceSlotConfigs execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAllianceSlotConfigs)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/alliance-deal-health")]
    public async Task<IActionResult> GetAllianceDealHealth()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAllianceDealHealth)} method...");

        try
        {
            var results = await _adminRepository.GetAllianceDealHealthAsync();

            _logger.LogInformation("GetAllianceDealHealth execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAllianceDealHealth)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/activity")]
    public async Task<IActionResult> GetDealActivity([FromQuery] int days = 14)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetDealActivity)} method...");

        try
        {
            var results = await _adminRepository.GetDealActivityAsync(days);

            _logger.LogInformation("GetDealActivity execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetDealActivity)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/cancellation-reasons")]
    public async Task<IActionResult> GetCancellationReasons([FromQuery] int days = 14)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetCancellationReasons)} method...");

        try
        {
            var results = await _adminRepository.GetCancellationReasonsAsync(days);

            _logger.LogInformation("GetCancellationReasons execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetCancellationReasons)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("settings/access")]
    [Authorize(Policy = Policies.ManageAdminAccess)]
    public async Task<IActionResult> GetAdminAccessSettings()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAdminAccessSettings)} method...");

        try
        {
            var results = await _adminRepository.GetAdminAccessSettingsAsync();

            _logger.LogInformation("GetAdminAccessSettings execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAdminAccessSettings)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpPost("settings/access")]
    [Authorize(Policy = Policies.ManageAdminAccess)]
    public async Task<IActionResult> CreateAdminAccess([FromBody] Valyria.Core.DTOs.AdminAccessCreateRequest request)
    {
        _logger.LogInformation($"Beginning execution for {nameof(CreateAdminAccess)} method...");

        try
        {
            var success = await _adminRepository.CreateAdminAccessAsync(request.AccountId, request.AdminRoleId);

            if (!success)
            {
                return BadRequest(new { error = "Failed to create admin access" });
            }

            _logger.LogInformation("CreateAdminAccess execution complete");

            return Ok(new { success = true });
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(CreateAdminAccess)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpDelete("settings/access")]
    [Authorize(Policy = Policies.ManageAdminAccess)]
    public async Task<IActionResult> DeleteAdminAccess([FromBody] Valyria.Core.DTOs.AdminAccessDeleteRequest request)
    {
        _logger.LogInformation($"Beginning execution for {nameof(DeleteAdminAccess)} method...");

        try
        {
            var success = await _adminRepository.DeleteAdminAccessAsync(request.AccountId, request.AdminRoleId);

            if (!success)
            {
                return BadRequest(new { error = "Failed to delete admin access" });
            }

            _logger.LogInformation("DeleteAdminAccess execution complete");

            return Ok(new { success = true });
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(DeleteAdminAccess)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }
}
