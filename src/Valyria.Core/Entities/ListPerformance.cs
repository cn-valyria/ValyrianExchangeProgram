namespace Valyria.Core.Entities;

public class ListPerformance
{
    public NationSimplified Nation { get; set; }
    public char Role { get; set; }
    public string ListType { get; set; }
    public DateOnly DayStarted { get; set; }
    public int DaysUntilRemoved { get; set; }
}