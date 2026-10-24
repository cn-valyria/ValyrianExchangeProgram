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
    private readonly IMassMessagesRepository _massMessagesRepository;

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
        IDiscordWebhookService discordWebhookService,
        IMassMessagesRepository massMessagesRepository)
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
        _massMessagesRepository = massMessagesRepository;
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
            var aidFileImported = await ImportAidFileAsync();
            await ImportWarFileAsync();

            _logger.LogInformation("All CN file imports completed");

            // Then update VEP deals against the freshly imported aid. Mass messages are built from
            // deal state and flags, so if this fails there's nothing safe to ping anyone about
            await UpdateDealStateAsync(aidFileImported);

            // A matching failure still fails the run, but shouldn't hold back today's reminders
            var matchingSucceeded = await TryMatchDealsAsync();

            // The loader runs many times a day, so only ping mass message recipients once per CN day,
            // when the new aid file comes in and deal states and flags are freshly updated
            if (aidFileImported)
                await SendMassMessageNotificationsAsync();

            if (!matchingSucceeded)
            {
                Environment.ExitCode = 1;
                return;
            }

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

    // Returns true if a new aid file was imported on this run
    private async Task<bool> ImportAidFileAsync()
    {
        // Pull today's file, and exit early if it's already been imported
        var (fileName, dataStream, fileAlreadyImported) = await GrabTodaysFile(CnFileType.Aid);
        if (fileAlreadyImported)
        {
            _logger.LogWarning("CN Nation file {fileName} already imported and cannot be imported again. Moving on...", fileName);
            return false;
        }

        // Upsert the data first. No point in uploading anything if this fails
        var parsedData = await RunDataParser<CnAidRecord>(dataStream);
        var dataForUpsert = parsedData.Select(x => x.Adapt<Aid>()).ToList();
        await UpsertDataToDb(dataForUpsert, fileName, _cnDataLoaderRepository.ImportAidAsync);

        // Now add the file to blob storage
        await UploadToBlobContainerAsync(_aidBlobClient, fileName, dataStream);
        return true;
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

    private async Task UpdateDealStateAsync(bool aidFileImported)
    {
        // Inactivity is measured against today's date, so only check it once today's aid file is in.
        // Activity tracking is safe to rerun
        if (aidFileImported)
            _logger.LogInformation("Updating deals: tracking activity and checking inactivity...");
        else
            _logger.LogInformation("No new aid file this run, so skipping the inactivity check. Tracking activity...");

        var result = await _vepDataLoaderRepository.UpdateDealStateAsync(checkInactivity: aidFileImported);
        _logger.LogInformation("Deals updated. {activityTracked} aid records linked to deals, {accountsHeld} accounts put on hold for deleted nations.",
            result.ActivityTracked, result.AccountsHeld);
    }

    private async Task<bool> TryMatchDealsAsync()
    {
        // Matching is safe to rerun, and picks up any admin changes
        _logger.LogInformation("Matching open slots...");

        try
        {
            var result = await _vepDataLoaderRepository.MatchDealsAsync();
            _logger.LogInformation("Matching completed. {dealsCreated} new deals created.", result.DealsCreated);

            // Let anyone who picked up a new partner know about it
            await _discordWebhookService.SendNewDealsNotificationAsync(result.NewPartnerDiscordIds);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while matching deals");
            return false;
        }
    }

    private async Task SendMassMessageNotificationsAsync()
    {
        _logger.LogInformation("Pinging today's mass message recipients on Discord...");

        var summary = await _massMessagesRepository.GetMassMessagesAsync();
        foreach (var message in summary.Messages)
        {
            var discordIds = message.Recipients
                .Select(r => r.DiscordUniqueId)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            _logger.LogInformation("{messageName} mass message: {recipientCount} recipients, {discordCount} with a Discord ID.",
                message.Name, message.Recipients.Count, discordIds.Count);

            await _discordWebhookService.SendMassMessageNotificationAsync(message.Code, discordIds);
        }
    }
}
