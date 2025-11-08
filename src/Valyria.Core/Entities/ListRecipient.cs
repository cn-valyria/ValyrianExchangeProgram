using Valyria.Core.Entities;

public class ListRecipient
{
    public NationSimplified Nation { get; set; }
    public int SlotsFree { get; set; }
    public int Credit { get; set; }
    public int Debt { get; set; }
    public RecentActivity RecentActivity { get; set; }
    public string Discord { get; set; }
}