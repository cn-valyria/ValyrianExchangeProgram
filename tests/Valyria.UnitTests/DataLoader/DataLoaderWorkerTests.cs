extern alias DataLoader;

using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using DataLoader::Valyria.Batch.DataLoader;
using Valyria.Core.DTOs;
using Valyria.Core.DTOs.CN;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Services.CnFileImport;
using Valyria.Infrastructure.Services.CnFileImport.Models;
using Valyria.Infrastructure.Services.Discord;

namespace Valyria.UnitTests.DataLoader;

public class DataLoaderWorkerTests : IDisposable
{
    private static readonly Dictionary<CnFileType, string> FileNames = new()
    {
        [CnFileType.Nations] = "CyberNations_SE_Nation_Stats_1092026510001",
        [CnFileType.Alliances] = "CyberNations_SE_Alliance_Stats_1092026515001",
        [CnFileType.Aid] = "CyberNations_SE_Aid_Stats_1092026520001",
        [CnFileType.War] = "CyberNations_SE_War_Stats_1092026525001",
    };

    private readonly IDataLoaderRepository _vepRepository = Substitute.For<IDataLoaderRepository>();
    private readonly IDataLoaderRepository _cnRepository = Substitute.For<IDataLoaderRepository>();
    private readonly ICnFileGrabber _grabber = Substitute.For<ICnFileGrabber>();
    private readonly ICnFileParser _parser = Substitute.For<ICnFileParser>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly IDiscordWebhookService _discord = Substitute.For<IDiscordWebhookService>();
    private readonly Dictionary<CnFileType, BlobContainerClient> _containers = new();
    private readonly Dictionary<CnFileType, BlobClient> _blobs = new();
    private readonly int _originalExitCode = Environment.ExitCode;

    public DataLoaderWorkerTests()
    {
        Mappings.EnsureConfigured();

        foreach (var (fileType, fileName) in FileNames)
        {
            _grabber.GetTodaysFileAsync(fileType)
                .Returns(_ => (fileName, (Stream)new MemoryStream(Encoding.UTF8.GetBytes($"{fileType} file contents"))));

            var blob = Substitute.For<BlobClient>();
            blob.ExistsAsync().Returns(Response.FromValue(false, Substitute.For<Response>()));
            var container = Substitute.For<BlobContainerClient>();
            container.GetBlobClient($"{fileName}.txt").Returns(blob);

            _blobs[fileType] = blob;
            _containers[fileType] = container;
        }

        _parser.ParseAsync<CnNationRecord>(Arg.Any<Stream>()).Returns(_ => Async(new CnNationRecord { NationId = 100, AllianceId = "1", WarStatus = "Peace Mode" }));
        _parser.ParseAsync<CnAllianceRecord>(Arg.Any<Stream>()).Returns(_ => Async(new CnAllianceRecord { Id = 1, Name = "Valyria" }));
        _parser.ParseAsync<CnAidRecord>(Arg.Any<Stream>()).Returns(_ => Async(new CnAidRecord { AidId = 900 }, new CnAidRecord { AidId = 901 }));
        _parser.ParseAsync<CnWarRecord>(Arg.Any<Stream>()).Returns(_ => Async(new CnWarRecord { WarId = 5000 }));

        _vepRepository.ProcessDealsAsync(Arg.Any<bool>()).Returns(new DealProcessingResult(3, 1, [111, 222]));
    }

    public void Dispose() => Environment.ExitCode = _originalExitCode;

    private static async IAsyncEnumerable<T> Async<T>(params T[] items)
    {
        foreach (var item in items)
            yield return item;
        await Task.CompletedTask;
    }

    private async Task RunWorker()
    {
        var worker = new DataLoaderWorker(
            NullLogger<DataLoaderWorker>.Instance,
            _vepRepository,
            _cnRepository,
            _grabber,
            _parser,
            _containers[CnFileType.Nations],
            _containers[CnFileType.Alliances],
            _containers[CnFileType.Aid],
            _containers[CnFileType.War],
            _lifetime,
            _discord);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!;
    }

    [Fact]
    public async Task ImportsEveryNewFile_ThenProcessesDealsWithTheInactivityCheck()
    {
        await RunWorker();

        await _cnRepository.Received(1).ImportNationsAsync(Arg.Is<IReadOnlyCollection<Nation>>(n => n.Single().NationId == 100), FileNames[CnFileType.Nations]);
        await _cnRepository.Received(1).ImportAlliancesAsync(Arg.Is<IReadOnlyCollection<Alliance>>(a => a.Single().Name == "Valyria"), FileNames[CnFileType.Alliances]);
        await _cnRepository.Received(1).ImportAidAsync(Arg.Is<IReadOnlyCollection<Aid>>(a => a.Select(x => x.Id).SequenceEqual(new[] { 900, 901 })), FileNames[CnFileType.Aid]);
        await _cnRepository.Received(1).ImportWarsAsync(Arg.Is<IReadOnlyCollection<War>>(w => w.Single().Id == 5000), FileNames[CnFileType.War]);

        foreach (var fileType in FileNames.Keys)
        {
            await _containers[fileType].Received(1).CreateIfNotExistsAsync();
            await _blobs[fileType].Received(1).UploadAsync(Arg.Is<Stream>(s => new StreamReader(s).ReadToEnd() == $"{fileType} file contents"));
        }

        await _vepRepository.Received(1).ProcessDealsAsync(checkInactivity: true);
        await _discord.Received(1).SendNewDealsNotificationAsync(Arg.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 111, 222 })));
        _lifetime.Received(1).StopApplication();
        Assert.Equal(_originalExitCode, Environment.ExitCode);
    }

    [Fact]
    public async Task SkipsFilesAlreadyImported_AndSkipsTheInactivityCheckWithoutANewAidFile()
    {
        _cnRepository.IsFileAlreadyImported(Arg.Any<string>()).Returns(true);

        await RunWorker();

        await _cnRepository.DidNotReceiveWithAnyArgs().ImportNationsAsync(default!, default!);
        await _cnRepository.DidNotReceiveWithAnyArgs().ImportAlliancesAsync(default!, default!);
        await _cnRepository.DidNotReceiveWithAnyArgs().ImportAidAsync(default!, default!);
        await _cnRepository.DidNotReceiveWithAnyArgs().ImportWarsAsync(default!, default!);
        Assert.All(_blobs.Values, blob => Assert.DoesNotContain(blob.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(BlobClient.UploadAsync)));

        await _vepRepository.Received(1).ProcessDealsAsync(checkInactivity: false);
        await _discord.Received(1).SendNewDealsNotificationAsync(Arg.Any<IReadOnlyCollection<long>>());
    }

    [Fact]
    public async Task DoesNotReuploadABlobThatAlreadyExists()
    {
        _blobs[CnFileType.Aid].ExistsAsync().Returns(Response.FromValue(true, Substitute.For<Response>()));

        await RunWorker();

        await _blobs[CnFileType.Aid].DidNotReceive().UploadAsync(Arg.Any<Stream>());
        await _blobs[CnFileType.War].Received(1).UploadAsync(Arg.Any<Stream>());
    }

    [Fact]
    public async Task StopsAndFailsTheRun_WhenADownloadFails()
    {
        _grabber.GetTodaysFileAsync(CnFileType.Nations).ThrowsAsync(new Exception("CN file could not be accessed."));

        await RunWorker();

        Assert.Equal(1, Environment.ExitCode);
        await _vepRepository.DidNotReceive().ProcessDealsAsync(Arg.Any<bool>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task DoesNotUploadAFile_WhoseDataFailedToSave()
    {
        _cnRepository.ImportAlliancesAsync(Arg.Any<IReadOnlyCollection<Alliance>>(), Arg.Any<string>())
            .ThrowsAsync(new Exception("deadlock"));

        await RunWorker();

        Assert.Equal(1, Environment.ExitCode);
        await _blobs[CnFileType.Nations].Received(1).UploadAsync(Arg.Any<Stream>());
        await _blobs[CnFileType.Alliances].DidNotReceive().UploadAsync(Arg.Any<Stream>());
        await _cnRepository.DidNotReceiveWithAnyArgs().ImportAidAsync(default!, default!);
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task FailsTheRun_WhenAFileCannotBeParsed()
    {
        _parser.ParseAsync<CnWarRecord>(Arg.Any<Stream>()).Returns(_ => throw new FormatException("bad row"));

        await RunWorker();

        Assert.Equal(1, Environment.ExitCode);
        await _cnRepository.DidNotReceiveWithAnyArgs().ImportWarsAsync(default!, default!);
        await _vepRepository.DidNotReceive().ProcessDealsAsync(Arg.Any<bool>());
    }
}
