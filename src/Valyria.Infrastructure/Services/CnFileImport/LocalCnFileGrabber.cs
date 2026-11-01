using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Valyria.Infrastructure.Services.CnFileImport;

/// <summary>
/// Serves a saved set of CN files from disk instead of downloading today's, for working offline or when CN is down.
/// Local development only (see the AppHost's CnFileSource setting).
/// </summary>
/// <remarks>
/// Each file is served under today's name, so the loader imports it again every CN half-day like a real file. Aid dates
/// are moved forward so the newest aid is dated today; otherwise the saved aid would fall outside the 10-day deal
/// windows within days of being saved.
/// </remarks>
public class LocalCnFileGrabber : ICnFileGrabber
{
    private const string AidDateColumn = "Date";
    private const string CnDateFormat = "M/d/yyyy h:mm:ss tt";

    // Latin-1 maps every byte to a character, so everything but the rewritten dates goes back out byte for byte
    private static readonly Encoding FileEncoding = Encoding.Latin1;
    private static readonly CultureInfo CnCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly string _folder;
    private readonly ILogger<LocalCnFileGrabber> _logger;

    public LocalCnFileGrabber(string folder, ILogger<LocalCnFileGrabber> logger)
    {
        _folder = folder;
        _logger = logger;
    }

    public async Task<(string FileName, Stream DataStream)> GetTodaysFileAsync(CnFileType fileType)
    {
        var cstNow = CnFileNames.CstNow;
        var fileName = CnFileNames.Todays(fileType, cstNow);

        // The saved file keeps its original name; only the type part has to match
        var typePrefix = fileName[..(fileName.IndexOf("_Stats_", StringComparison.Ordinal) + "_Stats_".Length)];
        var savedFile = Directory.GetFiles(_folder, $"{typePrefix}*.zip").OrderDescending().FirstOrDefault()
            ?? throw new FileNotFoundException($"No saved {fileType} file ({typePrefix}*.zip) in {_folder}");

        _logger.LogInformation("Serving saved CN file {savedFile} as {fileName}", Path.GetFileName(savedFile), fileName);

        string contents;
        await using (var zipFile = File.OpenRead(savedFile))
        using (var zip = new ZipArchive(zipFile, ZipArchiveMode.Read))
        using (var reader = new StreamReader(zip.Entries[0].Open(), FileEncoding))
            contents = await reader.ReadToEndAsync();

        if (fileType == CnFileType.Aid)
            contents = ShiftAidDates(contents, CnFileNames.CnDate(cstNow));

        return (fileName, new MemoryStream(FileEncoding.GetBytes(contents)));
    }

    /// <summary>
    /// Moves every aid date by the same number of days, so the newest one falls on <paramref name="cnDate"/>
    /// </summary>
    public static string ShiftAidDates(string contents, DateTime cnDate)
    {
        var lines = contents.Split('\n');
        var dateIndex = Array.IndexOf(lines[0].TrimEnd('\r').Split('|'), AidDateColumn);
        if (dateIndex < 0)
            throw new InvalidDataException($"Aid file has no {AidDateColumn} column");

        var rows = lines.Skip(1).Where(line => line.Trim().Length > 0).Select(line => line.Split('|')).ToList();
        if (rows.Count == 0)
            return contents;

        var newest = rows.Max(fields => DateTime.ParseExact(fields[dateIndex], CnDateFormat, CnCulture));
        var shift = cnDate.Date - newest.Date;
        if (shift == TimeSpan.Zero)
            return contents;

        var shifted = new StringBuilder(contents.Length);
        shifted.Append(lines[0]);
        foreach (var line in lines.Skip(1))
        {
            shifted.Append('\n');
            if (line.Trim().Length == 0)
            {
                shifted.Append(line);
                continue;
            }

            var fields = line.Split('|');
            fields[dateIndex] = DateTime.ParseExact(fields[dateIndex], CnDateFormat, CnCulture).Add(shift).ToString(CnDateFormat, CnCulture);
            shifted.Append(string.Join('|', fields));
        }

        return shifted.ToString();
    }
}
