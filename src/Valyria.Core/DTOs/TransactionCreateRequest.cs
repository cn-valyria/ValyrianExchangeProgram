namespace Valyria.Core.DTOs;

public class TransactionCreateRequest
{
    public int? AidId { get; set; }
    public int? SendingNationId { get; set; }
    public int? ReceivingNationId { get; set; }
    public string ReasonOverride { get; set; }
    public string Lu { get; set; }
    public int Classification { get; set; }
    public int Rate { get; set; }
    public int CashMovedTechCredit { get; set; }
    public int CashMovedCashCredit { get; set; }
    public int TechMovedCashCredit { get; set; }
    public int TechMovedTechCredit { get; set; }
}