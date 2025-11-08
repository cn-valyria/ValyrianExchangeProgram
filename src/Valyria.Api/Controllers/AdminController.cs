using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
}
