namespace Valyria.Core.DTOs;

public class TopScorerAccount
{
    public int Id { get; set; }
    public int NationId { get; set; }
    public string NationName { get; set; }
    public string RulerName { get; set; }
    public string AllianceName { get; set; }
    public int TotalTimesInDebt { get; set; }
    public decimal AvgDaysToClearDebt { get; set; }
    public int Rank { get; set; }
}
