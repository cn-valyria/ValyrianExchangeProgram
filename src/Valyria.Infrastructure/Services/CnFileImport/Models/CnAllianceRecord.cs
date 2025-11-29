using System.Globalization;
using CsvHelper.Configuration.Attributes;

namespace Valyria.Infrastructure.Services.CnFileImport.Models;

public class CnAllianceRecord
{
    [Name("Alliance")]
    public string Name { get; set; } = string.Empty;

    [Name("Alliance ID")]
    public int Id { get; set; }

    [Name("Updated")]
    public DateTime Updated { get; set; }

    [Name("Total Nations")]
    public int TotalNations { get; set; }

    [Name("Active Nations")]
    public int ActiveNations { get; set; }

    [Name("Percent Active")]
    public int PercentActive { get; set; }

    [Name("Strength")]
    public decimal Strength { get; set; }

    [Name("Average Strength")]
    public decimal AverageStrength { get; set; }

    [Name("Score")]
    [NumberStyles(NumberStyles.Float)]
    public decimal Score { get; set; }

    [Name("Land")]
    public decimal Land { get; set; }

    [Name("Infrastructure")]
    public decimal Infrastructure { get; set; }

    [Name("Technology")]
    public decimal Technology { get; set; }

    [Name("War")]
    public int War { get; set; }

    [Name("Peace")]
    public int Peace { get; set; }

    [Name("Soldiers")]
    public int Soldiers { get; set; }

    [Name("Tanks")]
    public int Tanks { get; set; }

    [Name("Cruise")]
    public int Cruise { get; set; }

    [Name("Nukes")]
    public int Nukes { get; set; }

    [Name("Aircraft")]
    public int Aircraft { get; set; }

    [Name("Navy")]
    public int Navy { get; set; }

    [Name("Anarchy")]
    public int Anarchy { get; set; }
}
