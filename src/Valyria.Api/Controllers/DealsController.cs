using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Core.Interfaces;
using Valyria.Core.DTOs;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DealsController : ControllerBase
{
    private readonly IDealsRepository _dealsRepository;
    private readonly ILogger<DealsController> _logger;

    public DealsController(
        IDealsRepository dealsRepository,
        ILogger<DealsController> logger)
    {
        _dealsRepository = dealsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all active deals for a specific account
    /// </summary>
    [HttpGet("account/{accountId}")]
    public async Task<IActionResult> GetAccountDeals(int accountId)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with accountId={AccountId}", nameof(GetAccountDeals), accountId);

        try
        {
            var result = await _dealsRepository.GetAccountDealsAsync(accountId);

            _logger.LogInformation("{MethodName} execution complete. Found {DealCount} active deals",
                nameof(GetAccountDeals), result.ActiveDeals.Count);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountDeals)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Get a page of every deal (active and cancelled) an account has been part of
    /// </summary>
    [HttpGet("account/{accountId}/history")]
    public async Task<IActionResult> GetAccountDealHistory(int accountId, [FromQuery] int? limit, [FromQuery] int? offset)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with accountId={AccountId}", nameof(GetAccountDealHistory), accountId);

        try
        {
            var result = await _dealsRepository.GetAccountDealHistoryAsync(accountId, limit ?? 10, offset ?? 0);

            _logger.LogInformation("{MethodName} execution complete. Found {TotalCount} total deals",
                nameof(GetAccountDealHistory), result.TotalCount);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountDealHistory)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Get the aid activity for a deal, from the perspective of one of its participants
    /// </summary>
    [HttpGet("{id}/activity")]
    public async Task<IActionResult> GetDealActivity(int id, [FromQuery] int accountId)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with dealId={DealId}, accountId={AccountId}",
            nameof(GetDealActivity), id, accountId);

        try
        {
            var result = await _dealsRepository.GetDealActivityAsync(id, accountId);

            if (result == null)
            {
                _logger.LogWarning("Deal with id={DealId} not found for accountId={AccountId}", id, accountId);
                return NotFound(new { error = $"Deal with id {id} not found" });
            }

            _logger.LogInformation("{MethodName} execution complete. Found {Count} activity records",
                nameof(GetDealActivity), result.Count);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetDealActivity)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Get details for a specific deal by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDeal(int id)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with dealId={DealId}", nameof(GetDeal), id);

        try
        {
            var result = await _dealsRepository.GetDealByIdAsync(id);

            if (result == null)
            {
                _logger.LogWarning("Deal with id={DealId} not found", id);
                return NotFound(new { error = $"Deal with id {id} not found" });
            }

            _logger.LogInformation("{MethodName} execution complete for dealId={DealId}", nameof(GetDeal), id);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetDeal)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Create a new deal between two slots
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateDeal([FromBody] CreateDealRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName}", nameof(CreateDeal));

        try
        {
            var username = User.Identity?.Name ?? "unknown";
            request.CreatedBy = username;

            var dealId = await _dealsRepository.CreateDealAsync(request);

            _logger.LogInformation("{MethodName} execution complete. Created deal with id={DealId}",
                nameof(CreateDeal), dealId);

            return CreatedAtAction(nameof(GetDeal), new { id = dealId }, new { id = dealId });
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(CreateDeal)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Cancel an existing deal
    /// </summary>
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelDeal(int id, [FromBody] CancelDealRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with dealId={DealId}", nameof(CancelDeal), id);

        try
        {
            var username = User.Identity?.Name ?? "unknown";
            await _dealsRepository.CancelDealAsync(id, request.Reason, username);

            _logger.LogInformation("{MethodName} execution complete for dealId={DealId}", nameof(CancelDeal), id);

            return NoContent();
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(CancelDeal)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Get a page of all active deals, optionally filtered by nation, ruler, or alliance name (admin only)
    /// </summary>
    [HttpGet("admin/active")]
    public async Task<IActionResult> GetActiveDeals([FromQuery] string? search, [FromQuery] int? limit, [FromQuery] int? offset)
    {
        _logger.LogInformation("Beginning execution for {MethodName}", nameof(GetActiveDeals));

        try
        {
            var result = await _dealsRepository.GetActiveDealsAsync(search, limit ?? 25, offset ?? 0);

            _logger.LogInformation("{MethodName} execution complete. Found {TotalCount} active deals",
                nameof(GetActiveDeals), result.TotalCount);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetActiveDeals)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Search all aid sent or received by VEP accounts (admin only)
    /// </summary>
    [HttpGet("admin/aid-activity")]
    public async Task<IActionResult> SearchAidActivity([FromQuery] AidActivityFilters filters, [FromQuery] int? limit, [FromQuery] int? offset)
    {
        _logger.LogInformation("Beginning execution for {MethodName}", nameof(SearchAidActivity));
        _logger.LogInformation("Received query params: {@query}", Request.Query);

        try
        {
            var result = await _dealsRepository.SearchAidActivityAsync(filters, limit ?? 25, offset ?? 0);

            _logger.LogInformation("{MethodName} execution complete. Found {TotalCount} aid records",
                nameof(SearchAidActivity), result.TotalCount);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(SearchAidActivity)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Get all accounts in the wait queue (admin only)
    /// </summary>
    [HttpGet("admin/wait-queue")]
    public async Task<IActionResult> GetWaitQueue()
    {
        _logger.LogInformation("Beginning execution for {MethodName}", nameof(GetWaitQueue));

        try
        {
            var result = await _dealsRepository.GetWaitQueueAsync();

            _logger.LogInformation("{MethodName} execution complete. Found {Count} accounts in wait queue",
                nameof(GetWaitQueue), result.Count);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetWaitQueue)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Get all flagged deals (admin only)
    /// </summary>
    [HttpGet("admin/flagged")]
    public async Task<IActionResult> GetFlaggedDeals()
    {
        _logger.LogInformation("Beginning execution for {MethodName}", nameof(GetFlaggedDeals));

        try
        {
            var result = await _dealsRepository.GetFlaggedDealsAsync();

            _logger.LogInformation("{MethodName} execution complete. Found {Count} flagged deals",
                nameof(GetFlaggedDeals), result.Count);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetFlaggedDeals)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Resolve a deal flag (admin only)
    /// </summary>
    [HttpPut("admin/flags/{flagId}/resolve")]
    public async Task<IActionResult> ResolveDealFlag(int flagId, [FromBody] ResolveFlagRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with flagId={FlagId}", nameof(ResolveDealFlag), flagId);

        try
        {
            var username = User.Identity?.Name ?? "unknown";
            await _dealsRepository.ResolveDealFlagAsync(flagId, username, request.AdminNotes);

            _logger.LogInformation("{MethodName} execution complete for flagId={FlagId}", nameof(ResolveDealFlag), flagId);

            return NoContent();
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(ResolveDealFlag)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }
}

public class CancelDealRequest
{
    public string Reason { get; set; }
}

public class ResolveFlagRequest
{
    public string? AdminNotes { get; set; }
}
