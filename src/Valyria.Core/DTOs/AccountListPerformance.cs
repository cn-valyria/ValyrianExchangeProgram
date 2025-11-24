namespace Valyria.Core.DTOs;

public class AccountListPerformance
{
    public int NationId { get; set; }
    public string RulerName { get; set; }
    public string NationName { get; set; }
    public string AllianceName { get; set; }
    public char Role { get; set; }
    public string ListType { get; set; }
    public DateTime DayStarted { get; set; }
    public int DaysUntilRemoved { get; set; }
}