using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Repository;
using Microsoft.Azure.Functions.Worker;
using api.Utils;

namespace api;

public class AdminEntryPoint
{
    private readonly IAdminRepository _adminRepository;

    public AdminEntryPoint(IAdminRepository adminRepository) => _adminRepository = adminRepository;

    [Function(nameof(GetDashboardData))]
    public async Task<IActionResult> GetDashboardData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard")] HttpRequest req,
        ILogger log)
    {
        log.LogInformation($"Beginning execution for {nameof(GetDashboardData)} method...");

        try
        {
            var results = await _adminRepository.GetAdminDashboardAsync();

            log.LogInformation("GetDashboardData execution complete");

            return new OkObjectResult(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetDashboardData)}", e);
            log.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }
}
