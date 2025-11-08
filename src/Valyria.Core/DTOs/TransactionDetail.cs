using System;

namespace Valyria.Core.DTOs;

public class TransactionDetail
{
    public int Id { get; set; }
    public int? AidId { get; set; }
    public int? SentByNationId { get; set; }
    public string SentByNationName { get; set; }
    public string SentByRulerName { get; set; }
    public string SentByAllianceName { get; set; }
    public int? ReceivedByNationId { get; set; }
    public string ReceivedByNationName { get; set; }
    public string ReceivedByRulerName { get; set; }
    public string ReceivedByAllianceName { get; set; }
    public int Status { get; set; }
    public int Money { get; set; }
    public int Technology { get; set; }
    public int Soldiers { get; set; }
    public string Reason { get; set; }
    public DateTime? StartsOn { get; set; }
    public string Lu { get; set; }
    public int Classification { get; set; }
    public int Rate { get; set; }
    public int CashMovedTechCredit { get; set; }
    public int CashMovedCashCredit { get; set; }
    public int TechMovedCashCredit { get; set; }
    public int TechMovedTechCredit { get; set; }
}