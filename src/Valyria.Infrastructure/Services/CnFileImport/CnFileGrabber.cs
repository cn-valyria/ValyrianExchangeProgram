using System.IO.Compression;
using Microsoft.Extensions.Logging;
using TimeZoneConverter;

namespace Valyria.Infrastructure.Services.CnFileImport;

public class CnFileGrabber : ICnFileGrabber
{
    private const string CnUrl = "https://www.cybernations.net";
    private readonly ILogger<CnFileGrabber> _logger;

    public CnFileGrabber(ILogger<CnFileGrabber> logger)
    {
        _logger = logger;
    }

    private DateTime CstNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZConvert.GetTimeZoneInfo("Central Standard Time"));

    public async Task<(string FileName, Stream DataStream)> GetTodaysFileAsync(CnFileType fileType)
    {
        var dataStream = new MemoryStream();

        _logger.LogInformation("Looking for a {fileType} CN file at UTC {utcNow}", fileType, DateTime.UtcNow);
        _logger.LogInformation("UTC time converted to {cstNow} CST", CstNow);

        var fileName = $"{GetCnFileName(fileType)}{GetTodaysDateAsString()}{GetCnFileExtension(fileType)}";

        _logger.LogInformation("File name: {fileName}", fileName);

        using (var client = new HttpClient())
        {
            var message = new HttpRequestMessage(HttpMethod.Get, $"{CnUrl}/assets/{fileName}.zip");
            var response = await client.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("CN file could not be accessed: {errorContent}", errorContent);
                throw new Exception("CN file could not be accessed.");
            }

            using (var responseData = await response.Content.ReadAsStreamAsync())
            using (var zip = new ZipArchive(responseData, ZipArchiveMode.Read))
            {
                var usefulFile = zip.Entries[0].Open();
                await usefulFile.CopyToAsync(dataStream);
                dataStream.Position = 0;
            }
        }

        return (fileName, dataStream);
    }

    private string GetCnFileName(CnFileType fileType) => fileType switch
    {
        CnFileType.Alliances => "CyberNations_SE_Alliance_Stats_",
        CnFileType.Nations => "CyberNations_SE_Nation_Stats_",
        CnFileType.Aid => "CyberNations_SE_Aid_Stats_",
        CnFileType.War => "CyberNations_SE_War_Stats_",
        _ => string.Empty
    };

    private string GetTodaysDateAsString()
    {
        var day = CstNow.Hour < 6 ? CstNow.Date.AddDays(-1) : CstNow.Date;
        return $"{day.Month}{day.Day}{day.Year}";
    }

    private string GetCnFileExtension(CnFileType fileType)
    {
        var firstPart = fileType switch
        {
            CnFileType.Alliances => "515",
            CnFileType.Nations => "510",
            CnFileType.Aid => "520",
            CnFileType.War => "525",
            _ => string.Empty
        };

        var lastPart = CstNow switch
        {
            // "morning" file (i.e. after 6am CST but before 6pm CST)
            _ when CstNow.Hour >= 6 && CstNow.Hour <= 18 => "001",
            // if it's not the "morning" file then it has to be the "evening" file
            _ => "002"
        };

        return firstPart + lastPart;
    }
}
