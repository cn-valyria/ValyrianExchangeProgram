using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Valyria.Core.Interfaces;

namespace Valyria.Batch.DataLoader;

public class DataLoaderWorker : BackgroundService
{
    private readonly ILogger<DataLoaderWorker> _logger;
    private readonly IDataLoaderRepository _dataLoaderRepository;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public DataLoaderWorker(
        ILogger<DataLoaderWorker> logger,
        IDataLoaderRepository dataLoaderRepository,
        IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _dataLoaderRepository = dataLoaderRepository;
        _applicationLifetime = applicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("VEP Data Loader started at: {time}", DateTimeOffset.Now);

            var transactionsLoaded = await LoadTodaysTransactionsAsync();
            if (transactionsLoaded > 0)
                await LoadTodaysListsAsync();

            _logger.LogInformation("VEP Data Loader completed at: {time}", DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while loading VEP data");
            Environment.ExitCode = 1;
        }
        finally
        {
            _applicationLifetime.StopApplication();
        }
    }

    private async Task<int> LoadTodaysTransactionsAsync()
    {
        var rowsAffected = await _dataLoaderRepository.LoadTodaysTransactionsAsync();
        _logger.LogInformation("Successfully loaded {rowsAffected} transactions.", rowsAffected);

        return rowsAffected;
    }

    private async Task LoadTodaysListsAsync()
    {
        await _dataLoaderRepository.LoadTodaysListsAsync();
        _logger.LogInformation("Successfully refreshed today's lists.");
    }
}
