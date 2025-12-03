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

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardData()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetDashboardData)} method...");

        try
        {
            var results = await _adminRepository.GetAdminDashboardAsync();

            _logger.LogInformation("GetDashboardData execution complete");

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetDashboardData)}", e);
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
