using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;
using Valyria.Core.DTOs;
using Valyria.Infrastructure.Exceptions;
using Valyria.Api.Auth;
using AutoMapper;
using Valyria.Core.Entities;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountsRepository _accountsRepository;
    private readonly ITransactionsRepository _transactionsRepository;
    private readonly IListsRepository _listsRepository;
    private readonly ITokenProvider _tokenProvider;
    private readonly IMapper _mapper;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(
        IAccountsRepository accountsRepository,
        ITransactionsRepository transactionsRepository,
        IListsRepository listsRepository,
        ITokenProvider tokenProvider,
        IMapper mapper,
        ILogger<AccountsController> logger)
    {
        _accountsRepository = accountsRepository;
        _transactionsRepository = transactionsRepository;
        _listsRepository = listsRepository;
        _tokenProvider = tokenProvider;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAccounts()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAllAccounts)} method...");

        try
        {
            var results = await _accountsRepository.GetAccountDetailsAsync();

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
        _logger.LogInformation($"Beginning execution for {nameof(GetAccount)} method...");

        try
        {
            var result = await _accountsRepository.GetAccountByIdAsync(id);

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

    [HttpGet("{id}/transactions")]
    public async Task<IActionResult> GetAccountTransactions(int id)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAccountTransactions)} method...");

        try
        {
            var results = await _transactionsRepository.GetTransactionsByAccount(id);

            _logger.LogInformation($"{nameof(GetAccountTransactions)} execution complete. Found {results.Count} results.");

            return Ok(results.Select(_mapper.Map<Transaction>));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountTransactions)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpGet("{id}/aidList")]
    public async Task<IActionResult> GetAccountAidList(int id)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAccountAidList)} method...");

        try
        {
            var results = await _listsRepository.GetListRecipientsForAccountAsync(id);

            _logger.LogInformation($"{nameof(GetAccountAidList)} execution complete. Found {results.Count} results.");

            return Ok(results.Select(_mapper.Map<ListRecipient>));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountAidList)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpPost]
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
}
