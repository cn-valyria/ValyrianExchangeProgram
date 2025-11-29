using CsvHelper.Configuration.Attributes;

namespace Valyria.Infrastructure.Services.CnFileImport.Models;

public class CnWarRecord
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

    [Name("War Status")]
    public string WarStatus { get; set; } = string.Empty;

    [Name("Begin Date")]
    public DateTime BeginDate { get; set; }

    [Name("End Date")]
    public DateTime EndDate { get; set; }

    [Name("Reason")]
    public string Reason { get; set; } = string.Empty;

    [Name("War ID")]
    public int WarId { get; set; }

    [Name("Destruction")]
    public decimal Destruction { get; set; }

    [Name("Attack Percent")]
    public int AttackPercent { get; set; }

    [Name("Defend Percent")]
    public int DefendPercent { get; set; }
}
