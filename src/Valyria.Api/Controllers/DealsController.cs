using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Api.Auth;
using Valyria.Core.Interfaces;
using Valyria.Core.DTOs;
using Valyria.Core.Entities;

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
        if (!User.CanAccessAccount(accountId))
            return Forbid();

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
        if (!User.CanAccessAccount(accountId))
            return Forbid();

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
        if (!User.CanAccessAccount(accountId))
            return Forbid();

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
    /// Get the open warning against an account on one of its deals, and the aid behind it
    /// </summary>
    [HttpGet("{id}/flag")]
    public async Task<IActionResult> GetAccountDealFlag(int id, [FromQuery] int accountId)
    {
        if (!User.CanAccessAccount(accountId))
            return Forbid();

        _logger.LogInformation("Beginning execution for {MethodName} with dealId={DealId}, accountId={AccountId}",
            nameof(GetAccountDealFlag), id, accountId);

        try
        {
            var result = await _dealsRepository.GetAccountDealFlagAsync(id, accountId);

            if (result == null)
            {
                _logger.LogWarning("No open flag on dealId={DealId} for accountId={AccountId}", id, accountId);
                return NotFound(new { error = $"No open flag on deal {id} for this account" });
            }

            _logger.LogInformation("{MethodName} execution complete. Found flag with id={FlagId}",
                nameof(GetAccountDealFlag), result.Flag.Id);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountDealFlag)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Get details for a specific deal by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = Policies.ViewAdminPortal)]
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
    [Authorize(Policy = Policies.ManageProgram)]
    public async Task<IActionResult> CreateDeal([FromBody] CreateDealRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName}", nameof(CreateDeal));

        try
        {
            request.CreatedBy = User.GetAuditName();

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
    [Authorize(Policy = Policies.ManageProgram)]
    public async Task<IActionResult> CancelDeal(int id, [FromBody] CancelDealRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with dealId={DealId}", nameof(CancelDeal), id);

        try
        {
            await _dealsRepository.CancelDealAsync(id, request.Reason, User.GetAuditName());

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
    [Authorize(Policy = Policies.ViewAdminPortal)]
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
    [Authorize(Policy = Policies.ViewAdminPortal)]
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
    [Authorize(Policy = Policies.ViewAdminPortal)]
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
    [Authorize(Policy = Policies.ViewAdminPortal)]
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
    /// Get the aid behind a deal flag: the deal's legs and what held the flagged nation's slots (admin only)
    /// </summary>
    [HttpGet("admin/flags/{flagId}/evidence")]
    [Authorize(Policy = Policies.ViewAdminPortal)]
    public async Task<IActionResult> GetDealFlagEvidence(int flagId)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with flagId={FlagId}", nameof(GetDealFlagEvidence), flagId);

        try
        {
            var result = await _dealsRepository.GetDealFlagEvidenceAsync(flagId);

            if (result == null)
            {
                _logger.LogWarning("Flag with id={FlagId} not found", flagId);
                return NotFound(new { error = $"Flag with id {flagId} not found" });
            }

            _logger.LogInformation("{MethodName} execution complete. Found {LegCount} legs and {SlotAidCount} aid in the flagged nation's slots",
                nameof(GetDealFlagEvidence), result.Legs.Count, result.FlaggedSlotAid.Count);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetDealFlagEvidence)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Give the account a warning is against more time before its deal is cancelled, resolving the warning (admin only)
    /// </summary>
    [HttpPut("admin/flags/{flagId}/extend")]
    [Authorize(Policy = Policies.ManageProgram)]
    public async Task<IActionResult> ExtendFlaggedDeal(int flagId, [FromBody] ExtendFlagRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with flagId={FlagId}, kind={Kind}",
            nameof(ExtendFlaggedDeal), flagId, request.Kind);

        try
        {
            var result = await _dealsRepository.ExtendFlaggedDealAsync(flagId, request.Kind, User.GetAuditName(), request.AdminNotes);

            switch (result)
            {
                case DealExtendResult.FlagNotFound:
                    _logger.LogWarning("Open flag with id={FlagId} not found", flagId);
                    return NotFound(new { error = $"Open flag with id {flagId} not found" });
                case DealExtendResult.NotExtendable:
                    _logger.LogWarning("Deal for flag with id={FlagId} can't be extended", flagId);
                    return Conflict(new { error = "This flag's deal can't be extended. It may have ended, or its account may no longer be holding it up." });
            }

            _logger.LogInformation("{MethodName} execution complete for flagId={FlagId}", nameof(ExtendFlaggedDeal), flagId);

            return NoContent();
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(ExtendFlaggedDeal)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Restore the deal a flag cancelled, resolving the deal's open flags (admin only)
    /// </summary>
    [HttpPut("admin/flags/{flagId}/restore")]
    [Authorize(Policy = Policies.ManageProgram)]
    public async Task<IActionResult> RestoreFlaggedDeal(int flagId, [FromBody] ResolveFlagRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with flagId={FlagId}", nameof(RestoreFlaggedDeal), flagId);

        try
        {
            var result = await _dealsRepository.RestoreFlaggedDealAsync(flagId, User.GetAuditName(), request.AdminNotes);

            switch (result)
            {
                case DealRestoreResult.FlagNotFound:
                    _logger.LogWarning("Open flag with id={FlagId} not found", flagId);
                    return NotFound(new { error = $"Open flag with id {flagId} not found" });
                case DealRestoreResult.NotRestorable:
                    _logger.LogWarning("Deal for flag with id={FlagId} can't be restored", flagId);
                    return Conflict(new { error = "This flag's deal can't be restored. Its slots or accounts may no longer be free." });
            }

            _logger.LogInformation("{MethodName} execution complete for flagId={FlagId}", nameof(RestoreFlaggedDeal), flagId);

            return NoContent();
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(RestoreFlaggedDeal)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    /// <summary>
    /// Resolve a deal flag (admin only)
    /// </summary>
    [HttpPut("admin/flags/{flagId}/resolve")]
    [Authorize(Policy = Policies.ManageProgram)]
    public async Task<IActionResult> ResolveDealFlag(int flagId, [FromBody] ResolveFlagRequest request)
    {
        _logger.LogInformation("Beginning execution for {MethodName} with flagId={FlagId}", nameof(ResolveDealFlag), flagId);

        try
        {
            await _dealsRepository.ResolveDealFlagAsync(flagId, User.GetAuditName(), request.AdminNotes);

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

public class ExtendFlagRequest
{
    public DealExtensionKind Kind { get; set; }
    public string? AdminNotes { get; set; }
}
