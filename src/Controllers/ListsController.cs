using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AutoMapper;
using Repository;
using Models;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ListsController : ControllerBase
{
    private readonly IListsRepository _listsRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<ListsController> _logger;

    public ListsController(IListsRepository listsRepository, IMapper mapper, ILogger<ListsController> logger)
    {
        _listsRepository = listsRepository;
        _mapper = mapper;
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

            return Ok(results.Select(_mapper.Map<AidList>));
        }
        catch (Exception e)
        {
            var wrapperException = new Exception($"Unexpected error occurred while executing {nameof(GetAllLists)}", e);
            _logger.LogError(e, wrapperException.Message);
            return StatusCode(500, new { error = wrapperException.Message, details = e.Message });
        }
    }
}
