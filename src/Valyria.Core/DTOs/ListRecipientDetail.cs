namespace Valyria.Core.DTOs;

public class ListRecipientDetail
{
    public int ListId { get; set; }
    public int AccountId { get; set; }
    public int NationId { get; set; }
    public string RulerName { get; set; }
    public string NationName { get; set; }
    public string AllianceName { get; set; }
    public int SlotsFree { get; set; }
    public int Credit { get; set; }
    public int Debt { get; set; }
    public int RecentActivity { get; set; }
    public string Discord { get; set; }
    public int DaysOnCurrentList { get; set; }
}