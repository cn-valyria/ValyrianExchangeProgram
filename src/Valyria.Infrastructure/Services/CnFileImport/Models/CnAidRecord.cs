using CsvHelper.Configuration.Attributes;

namespace Valyria.Infrastructure.Services.CnFileImport.Models;

public class CnAidRecord
{
    [Name("Declaring ID")]
    public int DeclaringId { get; set; }

    [Name("Declaring Ruler")]
    public string DeclaringRuler { get; set; } = string.Empty;

    [Name("Declaring Nation")]
    public string DeclaringNation { get; set; } = string.Empty;

    [Name("Declaring Alliance")]
    public string DeclaringAlliance { get; set; } = string.Empty;

    [Name("Declaring Alliance ID")]
    public int DeclaringAllianceId { get; set; }

    [Name("Declaring Team")]
    public string DeclaringTeam { get; set; } = string.Empty;

    [Name("Receiving ID")]
    public int ReceivingId { get; set; }

    [Name("Receiving Ruler")]
    public string ReceivingRuler { get; set; } = string.Empty;

    [Name("Receiving Nation")]
    public string ReceivingNation { get; set; } = string.Empty;

    [Name("Receiving Alliance")]
    public string ReceivingAlliance { get; set; } = string.Empty;

    [Name("Receiving Alliance ID")]
    public int ReceivingAllianceId { get; set; }

    [Name("Receiving Team")]
    public string ReceivingTeam { get; set; } = string.Empty;

    [Name("Status")]
    public string Status { get; set; } = string.Empty;

    [Name("Money")]
    public int Money { get; set; }

    [Name("Technology")]
    public int Technology { get; set; }

    [Name("Soldiers")]
    public int Soldiers { get; set; }

    [Name("Date")]
    public DateTime Date { get; set; }

    [Name("Reason")]
    public string Reason { get; set; } = string.Empty;

    [Name("Aid ID")]
    public int AidId { get; set; }
}
