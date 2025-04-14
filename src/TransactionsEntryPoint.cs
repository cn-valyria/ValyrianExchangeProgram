using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Repository;
using System.ComponentModel;
using AutoMapper;
using api.Contracts;
using System.Text.Json;
using Repository.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using api.Utils;
using Repository.DTO;

namespace api;

public class TransactionsEntryPoint
{
    private readonly ITransactionsRepository _transactionsRepository;
    private readonly IMapper _mapper;

    public TransactionsEntryPoint(ITransactionsRepository transactionsRepository, IMapper mapper)
        => (_transactionsRepository, _mapper) = (transactionsRepository, mapper);

    [Function(nameof(SearchTransactions))]
    public async Task<IActionResult> SearchTransactions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "transactions/{type}")] HttpRequest request,
        string type,
        ILogger log)
    {
        log.LogInformation($"Beginning execution for {nameof(SearchTransactions)} method...");
        log.LogInformation("Received query params: {@query}", request.Query);

        try
        {
            var transactionType = Enum.TryParse<TransactionType>(type, out var enumType) ? enumType : TransactionType.All;
            var filters = ConvertToFilters(request.Query);
            var limit = ConvertFromQueryString<int>(request.Query["limit"]);
            var offset = ConvertFromQueryString<int>(request.Query["offset"]);

            log.LogInformation("Parsed the filters {filters} with limit {limit} and offset {offset}", JsonSerializer.Serialize(filters), limit, offset);

            var searchResults = await _transactionsRepository.SearchTransactionsAsync(transactionType, filters, limit ?? 100, offset ?? 0);

            return new OkObjectResult(_mapper.Map<TransactionCollection>(searchResults));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(SearchTransactions)}", e);
            log.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(CreateTransaction))]
    public async Task<IActionResult> CreateTransaction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "transactions")] HttpRequest request,
        ILogger log)
    {
        log.LogInformation($"Beginning execution for {nameof(CreateTransaction)} method...");

        try
        {
            var newTransactionRequest = await JsonSerializer.DeserializeAsync<TransactionCreateRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            log.LogInformation("Received request to create transaction: {@transaction}", newTransactionRequest);

            var createdTransactionId = await _transactionsRepository.CreateTransactionAsync(newTransactionRequest);

            log.LogInformation("Successfully created transaction with ID {id}.", createdTransactionId);

            return new OkObjectResult(createdTransactionId);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(CreateTransaction)}", e);
            log.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(UpdateTransaction))]
    public async Task<IActionResult> UpdateTransaction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "transactions")] HttpRequest request,
        ILogger log)
    {
        log.LogInformation($"Beginning execution for {nameof(UpdateTransaction)} method...");

        try
        {
            var updateTransactionRequest = await JsonSerializer.DeserializeAsync<TransactionUpdateRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            log.LogInformation("Received request to update transaction: {@transaction}", updateTransactionRequest);

            await _transactionsRepository.UpdateTransactionAsync(updateTransactionRequest);

            log.LogInformation("Successfully updated transaction.");

            return new OkResult();
        }
        catch (NotFoundException e)
        {
            log.LogWarning(e.Message);
            return new NotFoundObjectResult(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(UpdateTransaction)}", e);
            log.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    [Function(nameof(DeleteTransaction))]
    public async Task<IActionResult> DeleteTransaction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "transactions/{id}")] HttpRequest request,
        int? id,
        ILogger log)
    {
        log.LogInformation($"Beginning execution for {nameof(DeleteTransaction)} method...");

        if (id is null)
        {
            log.LogError($"ID was not provided in the ${nameof(DeleteTransaction)} request.");
            return new BadRequestObjectResult("Must provide an ID to delete in the request");
        }

        try
        {
            await _transactionsRepository.DeleteTransactionAsync(id.Value);
            return new OkResult();
        }
        catch (NotFoundException e)
        {
            log.LogWarning(e.Message);
            return new NotFoundObjectResult(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(DeleteTransaction)}", e);
            log.LogError(e, wrapperException.Message);
            return new ExceptionResult(wrapperException, true);
        }
    }

    private TransactionFilters ConvertToFilters(IQueryCollection query) => new TransactionFilters
    {
        SentBy = query["sentBy"],
        ReceivedBy = query["receivedBy"],
        SentSince = ConvertFromQueryString<DateTime>(query["sentSince"]),
        SentUntil = ConvertFromQueryString<DateTime>(query["sentUntil"])
    };

    private T? ConvertFromQueryString<T>(string queryStringValue) where T : struct
    {
        if (string.IsNullOrEmpty(queryStringValue))
            return null;

        try
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (!(converter is null))
                return (T) converter.ConvertFromString(queryStringValue);

            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
