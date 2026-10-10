using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;
using Valyria.Core.DTOs;
using Valyria.Infrastructure.Exceptions;
using Valyria.Api.Auth;
using Mapster;
using Valyria.Core.Entities;
using Valyria.Core.Contracts;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountsRepository _accountsRepository;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(
        IAccountsRepository accountsRepository,
        ITokenProvider tokenProvider,
        ILogger<AccountsController> logger)
    {
        _accountsRepository = accountsRepository;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = Policies.ViewAdminPortal)]
    public async Task<IActionResult> GetAllAccounts([FromQuery] bool includeOnHold = false)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAllAccounts)} method...");

        try
        {
            var results = await _accountsRepository.GetAccountDetailsAsync(includeOnHold);

            _logger.LogInformation("GetAllAccounts execution complete. Found {count} results.", results.Count());

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAllAccounts)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAccount(int id)
    {
        if (!User.CanAccessAccount(id))
            return Forbid();

        _logger.LogInformation($"Beginning execution for {nameof(GetAccount)} method...");

        try
        {
            var result = await _accountsRepository.GetAccountByIdAsync(id);
            if (result is null)
                return NotFound($"No account found with ID {id}.");

            _logger.LogInformation("{methodName} execution complete. Returned {@result}.", nameof(GetAccount), result);

            return Ok(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageProgram)]
    public async Task<IActionResult> CreateAccount([FromBody] AccountCreateRequest newAccountRequest)
    {
        _logger.LogInformation($"Beginning execution for {nameof(CreateAccount)} method...");

        try
        {
            _logger.LogInformation($"Received request to create account: {{ nationId: {newAccountRequest.NationId} }}");

            var createdAccount = await _accountsRepository.CreateAccountAsync(newAccountRequest);

            _logger.LogInformation($"Successfully created account with ID: {createdAccount.Id}");

            return Ok(createdAccount);
        }
        catch (NotFoundException e)
        {
            _logger.LogWarning(e.Message);
            return NotFound(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(CreateAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpPut]
    [Authorize(Policy = Policies.ManageProgram)]
    public async Task<IActionResult> UpdateAccount([FromBody] AccountUpdateRequest accountToUpdate)
    {
        _logger.LogInformation($"Beginning execution for {nameof(UpdateAccount)} method...");

        try
        {
            await _accountsRepository.UpdateAccountAsync(accountToUpdate);

            return Ok();
        }
        catch (NotFoundException e)
        {
            _logger.LogWarning(e.Message);
            return NotFound(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(UpdateAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.ManageProgram)]
    public async Task<IActionResult> RemoveAccount(int id)
    {
        _logger.LogInformation($"Beginning execution for {nameof(RemoveAccount)} method...");

        try
        {
            await _accountsRepository.RemoveAccountAsync(id);
            return Ok();
        }
        catch (NotFoundException e)
        {
            _logger.LogWarning(e.Message);
            return NotFound(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(RemoveAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("prospect")]
    [Authorize(Policy = Policies.ManageProgram)]
    public async Task<IActionResult> FindProspectAccount([FromQuery] int nationId)
    {
        _logger.LogInformation($"Beginning execution for {nameof(FindProspectAccount)} method...");

        try
        {
            var prospectAccount = await _accountsRepository.FindProspectAccountAsync(nationId);
            if (prospectAccount is null)
            {
                var warning = $"No prospective merchant found by the provided parameters: {{ nationId: {nationId} }}.";
                _logger.LogWarning(warning);
                return NotFound(warning);
            }

            _logger.LogInformation($"Successfully found a prospect for the provided parameters: {{ nationId: {nationId} }}!");
            return Ok(prospectAccount);
        }
        catch (AlreadyExistsException e)
        {
            _logger.LogWarning(e.Message);
            return Conflict(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(FindProspectAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("topScorers")]
    public async Task<IActionResult> GetTopScorers()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetTopScorers)} method...");

        try
        {
            var results = await _accountsRepository.GetTopScorerAccountsAsync();

            _logger.LogInformation("GetTopScorers execution complete. Found {count} results.", results.Count());

            return Ok(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetTopScorers)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }
}
