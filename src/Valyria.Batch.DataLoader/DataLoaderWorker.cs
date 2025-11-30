using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Valyria.Core.DTOs.CN;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Services.CnFileImport;
using Valyria.Infrastructure.Services.CnFileImport.Models;

namespace Valyria.Batch.DataLoader;

public class DataLoaderWorker : BackgroundService
{
    private readonly ILogger<DataLoaderWorker> _logger;
    private readonly IDataLoaderRepository _vepDataLoaderRepository;
    private readonly IDataLoaderRepository _cnDataLoaderRepository;
    private readonly ICnFileGrabber _cnFileGrabber;
    private readonly ICnFileParser _cnFileParser;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public DataLoaderWorker(
        ILogger<DataLoaderWorker> logger,
        [FromKeyedServices("VepDb")] IDataLoaderRepository vepDataLoaderRepository,
        [FromKeyedServices("CnDb")] IDataLoaderRepository cnDataLoaderRepository,
        ICnFileGrabber cnFileGrabber,
        ICnFileParser cnFileParser,
        IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _vepDataLoaderRepository = vepDataLoaderRepository;
        _cnDataLoaderRepository = cnDataLoaderRepository;
        _cnFileGrabber = cnFileGrabber;
        _cnFileParser = cnFileParser;
        _applicationLifetime = applicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("CN/VEP Data Loader started at: {time}", DateTimeOffset.Now);

            // Import CN files first
            _logger.LogInformation("Starting CN file imports");

            await ImportNationsFileAsync();
            await ImportAlliancesFileAsync();
            await ImportAidFileAsync();
            await ImportWarFileAsync();

            _logger.LogInformation("All CN file imports completed");

            // Then load VEP data
            var transactionsLoaded = await LoadTodaysTransactionsAsync();
            if (transactionsLoaded > 0)
                await LoadTodaysListsAsync();

            _logger.LogInformation("CN/VEP Data Loader completed at: {time}", DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while loading CN/VEP data");
            Environment.ExitCode = 1;
        }
        finally
        {
            _applicationLifetime.StopApplication();
        }
    }

    private async Task ImportNationsFileAsync()
    {
        var (fileName, dataStream, fileAlreadyImported) = await GrabTodaysFile(CnFileType.Nations);
        if (fileAlreadyImported)
        {
            _logger.LogWarning("CN Nation file {fileName} already imported and cannot be imported again. Moving on...", fileName);
            return;
        }

        var parsedData = await RunDataParser<CnNationRecord>(dataStream);
        var dataForUpsert = parsedData.Select(x => x.Adapt<Nation>()).ToList();
        await UpsertDataToDb(dataForUpsert, fileName, _cnDataLoaderRepository.ImportNationsAsync);
    }

    private async Task ImportAlliancesFileAsync()
    {
        var (fileName, dataStream, fileAlreadyImported) = await GrabTodaysFile(CnFileType.Alliances);
        if (fileAlreadyImported)
        {
            _logger.LogWarning("CN Nation file {fileName} already imported and cannot be imported again. Moving on...", fileName);
            return;
        }

        var parsedData = await RunDataParser<CnAllianceRecord>(dataStream);
        var dataForUpsert = parsedData.Select(x => x.Adapt<Alliance>()).ToList();
        await UpsertDataToDb(dataForUpsert, fileName, _cnDataLoaderRepository.ImportAlliancesAsync);
    }

    private async Task ImportAidFileAsync()
    {
        var (fileName, dataStream, fileAlreadyImported) = await GrabTodaysFile(CnFileType.Aid);
        if (fileAlreadyImported)
        {
            _logger.LogWarning("CN Nation file {fileName} already imported and cannot be imported again. Moving on...", fileName);
            return;
        }

        var parsedData = await RunDataParser<CnAidRecord>(dataStream);
        var dataForUpsert = parsedData.Select(x => x.Adapt<Aid>()).ToList();
        await UpsertDataToDb(dataForUpsert, fileName, _cnDataLoaderRepository.ImportAidAsync);
    }

    private async Task ImportWarFileAsync()
    {
        var (fileName, dataStream, fileAlreadyImported) = await GrabTodaysFile(CnFileType.War);
        if (fileAlreadyImported)
        {
            _logger.LogWarning("CN Nation file {fileName} already imported and cannot be imported again. Moving on...", fileName);
            return;
        }

        var parsedData = await RunDataParser<CnWarRecord>(dataStream);
        var dataForUpsert = parsedData.Select(x => x.Adapt<War>()).ToList();
        await UpsertDataToDb(dataForUpsert, fileName, _cnDataLoaderRepository.ImportWarsAsync);
    }

    private async Task<(string, Stream, bool)> GrabTodaysFile(CnFileType fileType)
    {
        _logger.LogInformation("Importing CN {fileType} file.", fileType);

        var (fileName, dataStream) = await _cnFileGrabber.GetTodaysFileAsync(fileType);
        _logger.LogInformation("{fileType} file downloaded from CN.", fileType);

        var fileAlreadyImported = await _cnDataLoaderRepository.IsFileAlreadyImported(fileName);

        return (fileName, dataStream, fileAlreadyImported);
    }
        
    private async Task<List<T>> RunDataParser<T>(Stream fileBlob)
    {
        var allData = new List<T>();
        try
        {
            await foreach (var fileRecord in _cnFileParser.ParseAsync<T>(fileBlob))
                allData.Add(fileRecord);

            _logger.LogInformation("File successfully parsed. {count} records found.", allData.Count);
        }
        catch (Exception e)
        {
            _logger.LogError("Critical error encountered while parsing blob. \n Message: {message}\n StackTrace: {stackTrace}", e.Message, e.StackTrace);
            throw;
        }

        return allData;
    }
        
    private async Task UpsertDataToDb<T>(List<T> allData, string fileName, Func<IReadOnlyCollection<T>, string, Task> upsertFunc)
    {
        try
        {
            await upsertFunc(allData, fileName);
            _logger.LogInformation("Data uploaded successfully.");
        }
        catch (Exception e)
        {
            _logger.LogError("Critical error encountered while upserting aid data to the DB. \n Message: {message}\n StackTrace: {stackTrace}", e.Message, e.StackTrace);
            throw;
        }
    }

    private async Task<int> LoadTodaysTransactionsAsync()
    {
        var rowsAffected = await _vepDataLoaderRepository.LoadTodaysTransactionsAsync();
        _logger.LogInformation("Successfully loaded {rowsAffected} transactions.", rowsAffected);

        return rowsAffected;
    }

    private async Task LoadTodaysListsAsync()
    {
        await _vepDataLoaderRepository.LoadTodaysListsAsync();
        _logger.LogInformation("Successfully refreshed today's lists.");
    }
}
