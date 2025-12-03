using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mapster;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;
using Valyria.Core.Entities;
using Valyria.Core.Contracts;

namespace Valyria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ListsController : ControllerBase
{
    private readonly IListsRepository _listsRepository;
    private readonly ILogger<ListsController> _logger;

    public ListsController(IListsRepository listsRepository, ILogger<ListsController> logger)
    {
        _listsRepository = listsRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllLists()
    {
        _logger.LogInformation($"Beginning execution for {nameof(GetAllLists)} method...");

        try
        {
            var results = await _listsRepository.GetAidListsAsync();

            _logger.LogInformation($"{nameof(GetAllLists)} method completed successfully. Found {results.Count} results.");

            return Ok(results.Select(x => x.Adapt<AidList>()));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAllLists)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateList(int id, [FromBody] ListUpdateRequest updateRequest)
    {
        _logger.LogInformation($"Beginning execution for {nameof(UpdateList)} method...");

        try
        {
            await _listsRepository.UpdateListAsync(id, updateRequest.MessagesSentOn, updateRequest.MessagesSentByAccountId);
            var updatedList = await _listsRepository.GetAidListAsync(id);

            _logger.LogInformation($"{nameof(UpdateList)} method completed successfully.");

            return Ok(updatedList.Adapt<AidList>());
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAllLists)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }
}
