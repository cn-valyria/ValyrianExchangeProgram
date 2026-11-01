using Microsoft.Extensions.Logging.Abstractions;
using Valyria.Infrastructure.Services.CnFileImport;
using Valyria.Infrastructure.Services.CnFileImport.Models;

namespace Valyria.UnitTests.Infrastructure;

public class LocalCnFileGrabberTests
{
    private const string Header = "Declaring ID|Status|Date|Reason|Aid ID|";

    [Fact]
    public void ShiftAidDates_MovesTheNewestAidToTheCnDate_AndEveryOtherAidByTheSameAmount()
    {
        var contents = $"{Header}\r\n1|Approved|10/30/2026 5:18:43 AM|VEP|11|\r\n2|Pending|11/1/2026 11:00:00 PM|VEP|12|\r\n";

        var shifted = LocalCnFileGrabber.ShiftAidDates(contents, new DateTime(2026, 11, 4));

        Assert.Equal($"{Header}\r\n1|Approved|11/2/2026 5:18:43 AM|VEP|11|\r\n2|Pending|11/4/2026 11:00:00 PM|VEP|12|\r\n", shifted);
    }

    [Fact]
    public void ShiftAidDates_LeavesAFileFromTheCnDateAlone()
    {
        var contents = $"{Header}\n1|Approved|11/1/2026 5:18:43 AM|VEP|11|\n";

        Assert.Same(contents, LocalCnFileGrabber.ShiftAidDates(contents, new DateTime(2026, 11, 1)));
    }

    [Theory]
    [InlineData(CnFileType.Nations)]
    [InlineData(CnFileType.Alliances)]
    [InlineData(CnFileType.Aid)]
    [InlineData(CnFileType.War)]
    public async Task GetTodaysFileAsync_ServesTheSavedFileUnderTodaysName(CnFileType fileType)
    {
        var grabber = new LocalCnFileGrabber(SavedFilesFolder, NullLogger<LocalCnFileGrabber>.Instance);

        var (fileName, dataStream) = await grabber.GetTodaysFileAsync(fileType);

        Assert.Equal(CnFileNames.Todays(fileType, CnFileNames.CstNow), fileName);
        Assert.True(dataStream.Length > 0);
    }

    [Fact]
    public async Task GetTodaysFileAsync_SavedAidStillParses_WithTheNewestAidDatedToday()
    {
        var grabber = new LocalCnFileGrabber(SavedFilesFolder, NullLogger<LocalCnFileGrabber>.Instance);
        var (_, dataStream) = await grabber.GetTodaysFileAsync(CnFileType.Aid);

        var aid = new List<CnAidRecord>();
        await foreach (var record in new CnFileParser().ParseAsync<CnAidRecord>(dataStream))
            aid.Add(record);

        Assert.NotEmpty(aid);
        Assert.Equal(CnFileNames.CnDate(CnFileNames.CstNow), aid.Max(record => record.Date).Date);
    }

    // The committed set the AppHost's CnFileSource=Local serves
    private static string SavedFilesFolder
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Valyria.Aspire.sln")))
                directory = directory.Parent;

            return Path.Combine(directory!.FullName, "src", "Valyria.Database", "local", "cn-files");
        }
    }
}
