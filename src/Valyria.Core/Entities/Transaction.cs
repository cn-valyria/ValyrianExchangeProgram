using System;

namespace Valyria.Core.Entities;

public class Transaction
{
    public int Id { get; set; }
    public int? AidId { get; set; }
    public NationSimplified SentBy { get; set; }
    public NationSimplified ReceivedBy { get; set; }
    public AidStatus Status { get; set; }
    public int Money { get; set; }
    public int Technology { get; set; }
    public int Soldiers { get; set; }
    public string Reason { get; set; }
    public DateTime? StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
    public TransactionCode Code { get; set; }
    public int Classification { get; set; }
    public int Rate { get; set; }
    public int CashMovedTechCredit { get; set; }
    public int CashMovedCashCredit { get; set; }
    public int TechMovedCashCredit { get; set; }
    public int TechMovedTechCredit { get; set; }
}