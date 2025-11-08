using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Repository;
using System.ComponentModel;
using AutoMapper;
using api.Contracts;
using Repository.Infrastructure;
using Repository.DTO;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionsRepository _transactionsRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ITransactionsRepository transactionsRepository,
        IMapper mapper,
        ILogger<TransactionsController> logger)
    {
        _transactionsRepository = transactionsRepository;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet("{type}")]
    public async Task<IActionResult> SearchTransactions(string type)
    {
        _logger.LogInformation($"Beginning execution for {nameof(SearchTransactions)} method...");
        _logger.LogInformation("Received query params: {@query}", Request.Query);

        try
        {
            var transactionType = Enum.TryParse<TransactionType>(type, out var enumType) ? enumType : TransactionType.All;
            var filters = ConvertToFilters(Request.Query);
            var limit = ConvertFromQueryString<int>(Request.Query["limit"]);
            var offset = ConvertFromQueryString<int>(Request.Query["offset"]);

            _logger.LogInformation("Parsed the filters {filters} with limit {limit} and offset {offset}",
                System.Text.Json.JsonSerializer.Serialize(filters), limit, offset);

            var searchResults = await _transactionsRepository.SearchTransactionsAsync(transactionType, filters, limit ?? 100, offset ?? 0);

            return Ok(_mapper.Map<TransactionCollection>(searchResults));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(SearchTransactions)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] TransactionCreateRequest newTransactionRequest)
    {
        _logger.LogInformation($"Beginning execution for {nameof(CreateTransaction)} method...");

        try
        {
            _logger.LogInformation("Received request to create transaction: {@transaction}", newTransactionRequest);

            var createdTransactionId = await _transactionsRepository.CreateTransactionAsync(newTransactionRequest);

            _logger.LogInformation("Successfully created transaction with ID {id}.", createdTransactionId);

            return Ok(createdTransactionId);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(CreateTransaction)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdateTransaction([FromBody] TransactionUpdateRequest updateTransactionRequest)
    {
        _logger.LogInformation($"Beginning execution for {nameof(UpdateTransaction)} method...");

        try
        {
            _logger.LogInformation("Received request to update transaction: {@transaction}", updateTransactionRequest);

            await _transactionsRepository.UpdateTransactionAsync(updateTransactionRequest);

            _logger.LogInformation("Successfully updated transaction.");

            return Ok();
        }
        catch (NotFoundException e)
        {
            _logger.LogWarning(e.Message);
            return NotFound(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(UpdateTransaction)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        _logger.LogInformation($"Beginning execution for {nameof(DeleteTransaction)} method...");

        try
        {
            await _transactionsRepository.DeleteTransactionAsync(id);
            return Ok();
        }
        catch (NotFoundException e)
        {
            _logger.LogWarning(e.Message);
            return NotFound(e.Message);
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(DeleteTransaction)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
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
            if (converter is not null)
                return (T)converter.ConvertFromString(queryStringValue);

            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
