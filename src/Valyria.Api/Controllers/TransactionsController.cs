using Valyria.Core.Enums;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;
using System.ComponentModel;
using AutoMapper;
using Valyria.Core.Contracts;
using Valyria.Infrastructure.Exceptions;
using Valyria.Core.DTOs;

namespace Valyria.Api.Controllers;

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

    [HttpGet("{transactionType}")]
    public async Task<IActionResult> SearchTransactions(
        TransactionType transactionType,
        int? limit,
        int? offset,
        [FromBody] TransactionFilters filters)
    {
        _logger.LogInformation($"Beginning execution for {nameof(SearchTransactions)} method...");
        _logger.LogInformation("Received query params: {@query}", Request.Query);

        try
        {
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
}
