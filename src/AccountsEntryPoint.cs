using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Repository;
using Repository.DTO;
using Repository.Infrastructure;
using System.Linq;
using api.Auth;
using AutoMapper;
using Models;
using Microsoft.Azure.Functions.Worker;
using api.Utils;

namespace api;

public class AccountsEntryPoint
{
    private readonly IAccountsRepository _accountsRepository;
    private readonly ITransactionsRepository _transactionsRepository;
    private readonly IListsRepository _listsRepository;
    private readonly ITokenProvider _tokenProvider;
    private readonly IMapper _mapper;
    private readonly ILogger<AccountsEntryPoint> _logger;

    public AccountsEntryPoint(
        IAccountsRepository accountsRepository, 
        ITransactionsRepository transactionsRepository,
        IListsRepository listsRepository,
        ITokenProvider tokenProvider,
        IMapper mapper,
        ILogger<AccountsEntryPoint> logger)
    {
        _accountsRepository = accountsRepository;
        _transactionsRepository = transactionsRepository;
        _listsRepository = listsRepository;
        _tokenProvider = tokenProvider;
        _mapper = mapper;
        _logger = logger;
    }

    [Function(nameof(GetAllAccounts))]
    public async Task<IActionResult> GetAllAccounts([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "accounts")] HttpRequest req)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAllAccounts)} method...");

        // if (!_tokenProvider.ValidateToken(req.GetJwtBearerToken()))
        //     return new UnauthorizedResult();

        try
        {
            var results = await _accountsRepository.GetAccountDetailsAsync();

            _logger.LogInformation("GetAllAccounts execution complete. Found {count} results.", results.Count());

            return new OkObjectResult(results);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAllAccounts)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(GetAccount))]
    public async Task<IActionResult> GetAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "accounts/{id}")] HttpRequest request,
        int? id)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAccount)} method...");

        if (id is null)
            return new BadRequestObjectResult("Must provide an ID to delete in the request");

        try 
        {
            var result = await _accountsRepository.GetAccountByIdAsync(id.Value);

            _logger.LogInformation("{methodName} execution complete. Returned {@result}.", nameof(GetAccount), result);

            return new OkObjectResult(result);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(GetAccountTransactions))]
    public async Task<IActionResult> GetAccountTransactions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "accounts/{id}/transactions")] HttpRequest request,
        int? id)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAccountTransactions)} method...");

        if (id is null)
            return new BadRequestObjectResult("Must provide an ID to delete in the request");

        try 
        {
            var results = await _transactionsRepository.GetTransactionsByAccount(id.Value);

            _logger.LogInformation($"{nameof(GetAccountTransactions)} execution complete. Found {results.Count} results.");

            return new OkObjectResult(results.Select(_mapper.Map<Transaction>));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountTransactions)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(GetAccountAidList))]
    public async Task<IActionResult> GetAccountAidList(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "accounts/{id}/aidList")] HttpRequest request,
        int? id)
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAccountAidList)} method...");

        if (id is null)
            return new BadRequestObjectResult("Must provide an ID to delete in the request");

        try 
        {
            var results = await _listsRepository.GetListRecipientsForAccountAsync(id.Value);

            _logger.LogInformation($"{nameof(GetAccountAidList)} execution complete. Found {results.Count} results.");

            return new OkObjectResult(results.Select(_mapper.Map<ListRecipient>));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAccountAidList)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(CreateAccount))]
    public async Task<IActionResult> CreateAccount([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "accounts")] HttpRequest request)
    {
        _logger.LogInformation($"Beginning execution for {nameof(CreateAccount)} method...");

        try
        {
            var newAccountRequest = await JsonSerializer.DeserializeAsync<AccountCreateRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogInformation($"Received request to create account: {{ nationId: {newAccountRequest.NationId} }}");

            var createdAccount = await _accountsRepository.CreateAccountAsync(newAccountRequest);

            _logger.LogInformation($"Successfully created account with ID: {createdAccount.Id}");

            return new OkObjectResult(createdAccount);
        }
        catch (NotFoundException e)
        {
            _logger.LogWarning(e.Message);
            return new NotFoundObjectResult(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(UpdateAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(UpdateAccount))]
    public async Task<IActionResult> UpdateAccount([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "accounts")] HttpRequest request)
    {
        _logger.LogInformation($"Beginning execution for {nameof(UpdateAccount)} method...");

        try
        {
            var content = await new StreamReader(request.Body).ReadToEndAsync();
            var accountToUpdate = JsonSerializer.Deserialize<AccountUpdateRequest>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            await _accountsRepository.UpdateAccountAsync(accountToUpdate);

            return new OkResult();
        }
        catch (NotFoundException e)
        {
            _logger.LogWarning(e.Message);
            return new NotFoundObjectResult(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(UpdateAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(RemoveAccount))]
    public async Task<IActionResult> RemoveAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "accounts/{id}")] HttpRequest request,
        int? id)
    {
        _logger.LogInformation($"Beginning execution for {nameof(RemoveAccount)} method...");

        if (id is null)
            return new BadRequestObjectResult("Must provide an ID to delete in the request");

        try
        {
            await _accountsRepository.RemoveAccountAsync(id.Value);
            return new OkResult();
        }
        catch (NotFoundException e)
        {
            _logger.LogWarning(e.Message);
            return new NotFoundObjectResult(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(RemoveAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(FindProspectAccount))]
    public async Task<IActionResult> FindProspectAccount([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "accounts/prospect")] HttpRequest request)
    {
        _logger.LogInformation($"Beginning execution for {nameof(FindProspectAccount)} method...");

        string nationIdParameter = request.Query["nationId"];
        if (!int.TryParse(nationIdParameter, out var nationId))
            return new BadRequestObjectResult("Must provide a nationId parameter in the request");

        try
        {
            var prospectAccount = await _accountsRepository.FindProspectAccountAsync(nationId);
            if (prospectAccount is null)
            {
                var warning = $"No prospective merchant found by the provided parameters: {{ nationId: {nationId} }}.";
                _logger.LogWarning(warning);
                return new NotFoundObjectResult(warning);
            }

            _logger.LogInformation($"Successfully found a prospect for the provided parameters: {{ nationId: {nationId} }}!");
            return new OkObjectResult(prospectAccount);
        }
        catch (AlreadyExistsException e)
        {
            _logger.LogWarning(e.Message);
            return new ConflictObjectResult(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(FindProspectAccount)}", e);
            _logger.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }
}
