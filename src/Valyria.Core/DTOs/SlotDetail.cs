using System.Collections.Generic;
using Valyria.Core.Entities;

namespace Valyria.Core.DTOs;

public class SlotDetail
{
    public int Id { get; set; }
    public byte SlotNumber { get; set; }
    public int TechSentCycles { get; set; }
    public int TechReceivedCycles { get; set; }
    public int CashSentCycles { get; set; }
    public int CashReceivedCycles { get; set; }
    public AccountSimplified Account { get; set; }
    public bool HasActiveDeal { get; set; }
}

public class SlotUpdateRequest
{
    public int SlotId { get; set; }
    public int TechSentCycles { get; set; }
    public int TechReceivedCycles { get; set; }
    public int CashSentCycles { get; set; }
    public int CashReceivedCycles { get; set; }
}

public class AccountSlotsResponse
{
    public int AccountId { get; set; }
    public AccountSimplified Account { get; set; }
    public List<SlotDetail> Slots { get; set; } = new();
}
