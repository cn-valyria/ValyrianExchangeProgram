using System.Text;
using Valyria.Infrastructure.Services.CnFileImport;
using Valyria.Infrastructure.Services.CnFileImport.Models;

namespace Valyria.UnitTests.Infrastructure;

public class CnFileParserTests
{
    /// <summary>
    /// Builds a pipe-delimited CN file from header/value pairs, one dictionary per row
    /// </summary>
    internal static Stream CnFile(params Dictionary<string, string>[] rows)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join("|", rows[0].Keys));
        foreach (var row in rows)
            text.AppendLine(string.Join("|", row.Values));
        return new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
    }

    internal static Dictionary<string, string> AidRow(int aidId = 900, string status = "Approved") => new()
    {
        ["Declaring ID"] = "100", ["Declaring Ruler"] = "Bob", ["Declaring Nation"] = "Buyland",
        ["Declaring Alliance"] = "Valyria", ["Declaring Alliance ID"] = "1", ["Declaring Team"] = "Aqua",
        ["Receiving ID"] = "200", ["Receiving Ruler"] = "Sue", ["Receiving Nation"] = "Sellland",
        ["Receiving Alliance"] = "Valyria", ["Receiving Alliance ID"] = "1", ["Receiving Team"] = "Maroon",
        ["Status"] = status, ["Money"] = "3000000", ["Technology"] = "0", ["Soldiers"] = "0",
        ["Date"] = "10/9/2026 3:04:05 PM", ["Reason"] = "VEP", ["Aid ID"] = aidId.ToString()
    };

    internal static Dictionary<string, string> NationRow() => new()
    {
        ["Nation ID"] = "100", ["Ruler Name"] = "Bob", ["Nation Name"] = "Buyland", ["Alliance ID"] = "1",
        ["Alliance Date"] = "1/2/2020 12:00:00 AM", ["Alliance Status"] = "Member",
        ["Government Type"] = "Federal Government", ["Religion"] = "Bahai Faith", ["Team"] = "Aqua",
        ["Created"] = "1/1/2020 12:00:00 AM", ["Technology"] = "1500.25", ["Infrastructure"] = "8000.5",
        ["Base Land"] = "3000", ["War Status"] = "Peace Mode", ["Resource 1"] = "Gold", ["Resource 2"] = "Iron",
        ["Votes"] = "0", ["Strength"] = "12,345.678", ["DEFCON"] = "5", ["Base Soldiers"] = "-1,234",
        ["Tanks"] = "2,000", ["Cruise"] = "10", ["Nukes"] = "0", ["Activity"] = "Active In The Last 3 Days",
        ["Connected Resource 1"] = "Oil", ["Connected Resource 2"] = "", ["Connected Resource 3"] = "",
        ["Connected Resource 4"] = "", ["Connected Resource 5"] = "", ["Connected Resource 6"] = "",
        ["Connected Resource 7"] = "", ["Connected Resource 8"] = "", ["Connected Resource 9"] = "",
        ["Connected Resource 10"] = ""
    };

    internal static Dictionary<string, string> AllianceRow() => new()
    {
        ["Alliance"] = "Valyria", ["Alliance ID"] = "1", ["Updated"] = "10/9/2026 12:00:00 AM",
        ["Total Nations"] = "50", ["Active Nations"] = "45", ["Percent Active"] = "90",
        ["Strength"] = "1234567.6", ["Average Strength"] = "24691.4", ["Score"] = "1.2345E+1",
        ["Land"] = "150000.5", ["Infrastructure"] = "400000.4", ["Technology"] = "75000.6",
        ["War"] = "2", ["Peace"] = "48", ["Soldiers"] = "100000", ["Tanks"] = "5000", ["Cruise"] = "100",
        ["Nukes"] = "250", ["Aircraft"] = "900", ["Navy"] = "300", ["Anarchy"] = "1"
    };

    internal static Dictionary<string, string> WarRow() => new()
    {
        ["Declaring ID"] = "100", ["Declaring Ruler"] = "Bob", ["Declaring Nation"] = "Buyland",
        ["Declaring Alliance"] = "Valyria", ["Declaring Alliance ID"] = "1", ["Declaring Team"] = "Aqua",
        ["Receiving ID"] = "300", ["Receiving Ruler"] = "Eve", ["Receiving Nation"] = "Raidland",
        ["Receiving Alliance"] = "Rogues", ["Receiving Alliance ID"] = "2", ["Receiving Team"] = "Black",
        ["War Status"] = "Active", ["Begin Date"] = "10/1/2026 12:00:00 AM", ["End Date"] = "10/8/2026 12:00:00 AM",
        ["Reason"] = "Raid", ["War ID"] = "5000", ["Destruction"] = "12.5", ["Attack Percent"] = "60",
        ["Defend Percent"] = "40"
    };

    private static async Task<List<T>> Parse<T>(Stream stream)
    {
        var records = new List<T>();
        await foreach (var record in new CnFileParser().ParseAsync<T>(stream))
            records.Add(record);
        return records;
    }

    [Fact]
    public async Task ParsesEveryRow()
    {
        var records = await Parse<CnAidRecord>(CnFile(AidRow(900), AidRow(901, "Pending")));

        Assert.Equal([900, 901], records.Select(r => r.AidId));
        Assert.Equal("Pending", records[1].Status);
        Assert.Equal(3_000_000, records[0].Money);
        Assert.Equal(new DateTime(2026, 10, 9, 15, 4, 5), records[0].Date);
    }

    [Fact]
    public async Task ParsesThousandsSeparatorsAndSignsInNationStats()
    {
        var nation = Assert.Single(await Parse<CnNationRecord>(CnFile(NationRow())));

        Assert.Equal(12_345.678m, nation.Strength);
        Assert.Equal(-1_234, nation.BaseSoldiers);
        Assert.Equal(2_000, nation.Tanks);
        Assert.Equal(new DateTime(2020, 1, 2), nation.AllianceDate);
    }

    [Fact]
    public async Task ParsesScientificNotationInAllianceScore()
    {
        var alliance = Assert.Single(await Parse<CnAllianceRecord>(CnFile(AllianceRow())));

        Assert.Equal(12.345m, alliance.Score);
        Assert.Equal("Valyria", alliance.Name);
    }

    [Fact]
    public async Task ParsesWars()
    {
        var war = Assert.Single(await Parse<CnWarRecord>(CnFile(WarRow())));

        Assert.Equal(5000, war.WarId);
        Assert.Equal(12.5m, war.Destruction);
    }
}
