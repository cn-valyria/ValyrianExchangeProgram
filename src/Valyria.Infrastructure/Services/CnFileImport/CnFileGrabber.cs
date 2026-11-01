using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace Valyria.Infrastructure.Services.CnFileImport;

public class CnFileGrabber : ICnFileGrabber
{
    private const string CnUrl = "https://www.cybernations.net";
    private readonly ILogger<CnFileGrabber> _logger;

    public CnFileGrabber(ILogger<CnFileGrabber> logger)
    {
        _logger = logger;
    }

    public async Task<(string FileName, Stream DataStream)> GetTodaysFileAsync(CnFileType fileType)
    {
        var dataStream = new MemoryStream();
        var cstNow = CnFileNames.CstNow;

        _logger.LogInformation("Looking for a {fileType} CN file at UTC {utcNow}", fileType, DateTime.UtcNow);
        _logger.LogInformation("UTC time converted to {cstNow} CST", cstNow);

        var fileName = CnFileNames.Todays(fileType, cstNow);

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
}
