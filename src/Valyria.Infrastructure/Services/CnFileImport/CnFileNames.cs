using TimeZoneConverter;

namespace Valyria.Infrastructure.Services.CnFileImport;

/// <summary>
/// How CN names its daily files, e.g. CyberNations_SE_Nation_Stats_1112026510001
/// </summary>
public static class CnFileNames
{
    public static DateTime CstNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZConvert.GetTimeZoneInfo("Central Standard Time"));

    /// <summary>
    /// The CN date today's files are for. CN doesn't roll over to the new day's files until 6am CST
    /// </summary>
    public static DateTime CnDate(DateTime cstNow) => cstNow.Hour < 6 ? cstNow.Date.AddDays(-1) : cstNow.Date;

    public static string Todays(CnFileType fileType, DateTime cstNow)
    {
        var day = CnDate(cstNow);
        return $"{GetCnFileName(fileType)}{day.Month}{day.Day}{day.Year}{GetCnFileExtension(fileType, cstNow)}";
    }

    private static string GetCnFileName(CnFileType fileType) => fileType switch
    {
        CnFileType.Alliances => "CyberNations_SE_Alliance_Stats_",
        CnFileType.Nations => "CyberNations_SE_Nation_Stats_",
        CnFileType.Aid => "CyberNations_SE_Aid_Stats_",
        CnFileType.War => "CyberNations_SE_War_Stats_",
        _ => string.Empty
    };

    private static string GetCnFileExtension(CnFileType fileType, DateTime cstNow)
    {
        var firstPart = fileType switch
        {
            CnFileType.Alliances => "515",
            CnFileType.Nations => "510",
            CnFileType.Aid => "520",
            CnFileType.War => "525",
            _ => string.Empty
        };

        var lastPart = cstNow switch
        {
            // "morning" file (i.e. after 12am CST but before 12pm CST)
            _ when cstNow.Hour >= 0 && cstNow.Hour <= 12 => "001",
            // if it's not the "morning" file then it has to be the "evening" file
            _ => "002"
        };

        return firstPart + lastPart;
    }
}
