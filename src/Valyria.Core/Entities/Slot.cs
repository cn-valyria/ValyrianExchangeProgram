using System;

namespace Valyria.Core.Entities;

public class Slot
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public byte SlotNumber { get; set; }
    public int TechSentCycles { get; set; }
    public int TechReceivedCycles { get; set; }
    public int CashSentCycles { get; set; }
    public int CashReceivedCycles { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }
    public string UpdatedBy { get; set; }
}
