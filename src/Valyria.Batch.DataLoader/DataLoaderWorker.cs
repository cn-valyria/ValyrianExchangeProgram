using Azure.Storage.Blobs;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Valyria.Batch.DataLoader.Extensions;
using Valyria.Core.DTOs.CN;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Services.CnFileImport;
using Valyria.Infrastructure.Services.CnFileImport.Models;
using Valyria.Infrastructure.Services.Discord;

namespace Valyria.Batch.DataLoader;

public class DataLoaderWorker : BackgroundService
{
    private readonly ILogger<DataLoaderWorker> _logger;
    private readonly IDataLoaderRepository _vepDataLoaderRepository;
    private readonly IDataLoaderRepository _cnDataLoaderRepository;
    private readonly ICnFileGrabber _cnFileGrabber;
    private readonly ICnFileParser _cnFileParser;
    private readonly BlobContainerClient _nationBlobClient;
    private readonly BlobContainerClient _allianceBlobClient;
    private readonly BlobContainerClient _aidBlobClient;
    private readonly BlobContainerClient _warBlobClient;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IDiscordWebhookService _discordWebhookService;

    // Should be roughly 12am - 12pm CST. I'm just lazy and don't feel like redoing the CnFileGrabber logic
    private bool IsMorningRun => DateTime.UtcNow.Hour >= 6 && DateTime.UtcNow.Hour <= 18;

    public DataLoaderWorker(
        ILogger<DataLoaderWorker> logger,
        [FromKeyedServices("VepDb")] IDataLoaderRepository vepDataLoaderRepository,
        [FromKeyedServices("CnDb")] IDataLoaderRepository cnDataLoaderRepository,
        ICnFileGrabber cnFileGrabber,
        ICnFileParser cnFileParser,
        [FromKeyedServices("nation")] BlobContainerClient nationBlobClient,
        [FromKeyedServices("alliance")] BlobContainerClient allianceBlobClient,
        [FromKeyedServices("aid")] BlobContainerClient aidBlobClient,
        [FromKeyedServices("war")] BlobContainerClient warBlobClient,
        IHostApplicationLifetime applicationLifetime,
        IDiscordWebhookService discordWebhookService)
    {
        _logger = logger;
        _vepDataLoaderRepository = vepDataLoaderRepository;
        _cnDataLoaderRepository = cnDataLoaderRepository;
        _cnFileGrabber = cnFileGrabber;
        _cnFileParser = cnFileParser;
        _nationBlobClient = nationBlobClient;
        _allianceBlobClient = allianceBlobClient;
        _aidBlobClient = aidBlobClient;
        _warBlobClient = warBlobClient;
        _applicationLifetime = applicationLifetime;
        _discordWebhookService = discordWebhookService;
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
        // Pull today's file, and exit early if it's already been imported
        var (fileName, dataStream, fileAlreadyImported) = await GrabTodaysFile(CnFileType.Nations);
        if (fileAlreadyImported)
        {
            _logger.LogWarning("CN Nation file {fileName} already imported and cannot be imported again. Moving on...", fileName);
            return;
        }

        // Upsert the data first. No point in uploading anything if this fails
        var parsedData = await RunDataParser<CnNationRecord>(dataStream);
        var dataForUpsert = parsedData.Select(x => x.Adapt<Nation>()).ToList();
        await UpsertDataToDb(dataForUpsert, fileName, _cnDataLoaderRepository.ImportNationsAsync);

        // Now add the file to blob storage
        await UploadToBlobContainerAsync(_nationBlobClient, fileName, dataStream);
    }

    private async Task ImportAlliancesFileAsync()
    {
        // Pull today's file, and exit early if it's already been imported
        var (fileName, dataStream, fileAlreadyImported) = await GrabTodaysFile(CnFileType.Alliances);
        if (fileAlreadyImported)
        {
            _logger.LogWarning("CN Nation file {fileName} already imported and cannot be imported again. Moving on...", fileName);
            return;
        }

        // Upsert the data first. No point in uploading anything if this fails
        var parsedData = await RunDataParser<CnAllianceRecord>(dataStream);
        var dataForUpsert = parsedData.Select(x => x.Adapt<Alliance>()).ToList();
        await UpsertDataToDb(dataForUpsert, fileName, _cnDataLoaderRepository.ImportAlliancesAsync);

        // Now add the file to blob storage
        await UploadToBlobContainerAsync(_allianceBlobClient, fileName, dataStream);
    }

    private async Task ImportAidFileAsync()
    {
        // Pull today's file, and exit early if it's already been imported
        var (fileName, dataStream, fileAlreadyImported) = await GrabTodaysFile(CnFileType.Aid);
        if (fileAlreadyImported)
        {
            _logger.LogWarning("CN Nation file {fileName} already imported and cannot be imported again. Moving on...", fileName);
            return;
        }

        // Upsert the data first. No point in uploading anything if this fails
        var parsedData = await RunDataParser<CnAidRecord>(dataStream);
        var dataForUpsert = parsedData.Select(x => x.Adapt<Aid>()).ToList();
        await UpsertDataToDb(dataForUpsert, fileName, _cnDataLoaderRepository.ImportAidAsync);

        // Now add the file to blob storage
        await UploadToBlobContainerAsync(_aidBlobClient, fileName, dataStream);
    }

    private async Task ImportWarFileAsync()
    {
        // Pull today's file, and exit early if it's already been imported
        var (fileName, dataStream, fileAlreadyImported) = await GrabTodaysFile(CnFileType.War);
        if (fileAlreadyImported)
        {
            _logger.LogWarning("CN Nation file {fileName} already imported and cannot be imported again. Moving on...", fileName);
            return;
        }

        // Upsert the data first. No point in uploading anything if this fails
        var parsedData = await RunDataParser<CnWarRecord>(dataStream);
        var dataForUpsert = parsedData.Select(x => x.Adapt<War>()).ToList();
        await UpsertDataToDb(dataForUpsert, fileName, _cnDataLoaderRepository.ImportWarsAsync);

        // Now add the file to blob storage
        await UploadToBlobContainerAsync(_warBlobClient, fileName, dataStream);
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
            await foreach (var fileRecord in _cnFileParser.ParseAsync<T>(await fileBlob.CopyAsync()))
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

    private async Task UploadToBlobContainerAsync(BlobContainerClient blobContainerClient, string fileName, Stream dataStream)
    {
        _logger.LogInformation("Uploading {fileName} to blob storage", fileName);

        await blobContainerClient.CreateIfNotExistsAsync();
        var newFileBlob = blobContainerClient.GetBlobClient($"{fileName}.txt");
        if (!await newFileBlob.ExistsAsync())
            await newFileBlob.UploadAsync(await dataStream.CopyAsync());

        _logger.LogInformation("{fileName} uploaded to blob storage successfully", fileName);
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
        if (IsMorningRun)
        {
            _logger.LogInformation("It's a new day, so loading new lists...");
            await _vepDataLoaderRepository.LoadTodaysListsAsync();

            // Send Discord notification for outgoing aid lists after successful refresh
            await _discordWebhookService.SendListNotificationAsync(1);
            await _discordWebhookService.SendListNotificationAsync(3);
        }
        else
        {
            _logger.LogInformation("It's later in the day when lists were generated, so just remove accounts with full slots.");
            await _vepDataLoaderRepository.RemoveAccountsWithFullSlotsFromListsAsync();
        }
        _logger.LogInformation("Successfully refreshed today's lists.");
    }
}
