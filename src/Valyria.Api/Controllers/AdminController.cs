using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IAdminRepository _adminRepository;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminRepository adminRepository, ILogger<AdminController> logger)
    {
        _adminRepository = adminRepository;
        _logger = logger;
    }

    [HttpGet("dashboard/general-program-summary")]
    public async Task<IActionResult> GetGeneralProgramSummary()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetGeneralProgramSummary)} method...");

        try
        {
            var results = await _adminRepository.GetGeneralProgramSummaryAsync();

            _logger.LogInformation("GetGeneralProgramSummary execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetGeneralProgramSummary)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/slot-usage-summary")]
    public async Task<IActionResult> GetSlotUsageSummary()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetSlotUsageSummary)} method...");

        try
        {
            var results = await _adminRepository.GetSlotUsageSummaryAsync();

            _logger.LogInformation("GetSlotUsageSummary execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetSlotUsageSummary)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/unused-credit-summary")]
    public async Task<IActionResult> GetUnusedCreditSummary()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetUnusedCreditSummary)} method...");

        try
        {
            var results = await _adminRepository.GetUnusedCreditSummaryAsync();

            _logger.LogInformation("GetUnusedCreditSummary execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetUnusedCreditSummary)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/aid-ratio-summary")]
    public async Task<IActionResult> GetAidRatioSummary()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAidRatioSummary)} method...");

        try
        {
            var results = await _adminRepository.GetAidRatioSummaryAsync();

            _logger.LogInformation("GetAidRatioSummary execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAidRatioSummary)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/transactions-per-day")]
    public async Task<IActionResult> GetTransactionsPerDay()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetTransactionsPerDay)} method...");

        try
        {
            var results = await _adminRepository.GetTransactionsPerDayAsync();

            _logger.LogInformation("GetTransactionsPerDay execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetTransactionsPerDay)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/pending-credit-per-alliance")]
    public async Task<IActionResult> GetPendingCreditPerAlliance()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetPendingCreditPerAlliance)} method...");

        try
        {
            var results = await _adminRepository.GetPendingCreditPerAllianceAsync();

            _logger.LogInformation("GetPendingCreditPerAlliance execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetPendingCreditPerAlliance)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/accounts-per-role")]
    public async Task<IActionResult> GetAccountsPerRole()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAccountsPerRole)} method...");

        try
        {
            var results = await _adminRepository.GetAccountsPerRoleAsync();

            _logger.LogInformation("GetAccountsPerRole execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountsPerRole)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/total-slots-per-alliance")]
    public async Task<IActionResult> GetTotalSlotsPerAlliance()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetTotalSlotsPerAlliance)} method...");

        try
        {
            var results = await _adminRepository.GetTotalSlotsPerAllianceAsync();

            _logger.LogInformation("GetTotalSlotsPerAlliance execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetTotalSlotsPerAlliance)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/free-slots-per-list")]
    public async Task<IActionResult> GetFreeSlotsPerList()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetFreeSlotsPerList)} method...");

        try
        {
            var results = await _adminRepository.GetFreeSlotsPerListAsync();

            _logger.LogInformation("GetFreeSlotsPerList execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetFreeSlotsPerList)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("dashboard/accounts-per-alliance")]
    public async Task<IActionResult> GetAccountsPerAlliance()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAccountsPerAlliance)} method...");

        try
        {
            var results = await _adminRepository.GetAccountsPerAllianceAsync();

            _logger.LogInformation("GetAccountsPerAlliance execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountsPerAlliance)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("settings/access")]
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
