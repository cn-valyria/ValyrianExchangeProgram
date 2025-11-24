using Valyria.Core.Entities;

namespace Valyria.Core.DTOs;

public class AccountDetail
{
    public int Id { get; set; }
    public int NationId { get; set; }
    public string NationName { get; set; }
    public string RulerName { get; set; }
    public string AllianceName { get; set; }
    public string Discord { get; set; }
    public long DiscordUniqueId { get; set; }
    public string UniqueCode { get; set; }
    public char Role { get; set; }
    public RecentActivity Activity { get; set; }
    public decimal Strength { get; set; }
    public decimal Infra { get; set; }
    public decimal Tech { get; set; }
    public NationalWarStatus WarStatus { get; set; }
    public bool HasForeignMinistry { get; set; }
    public bool HasFederalAidCommission { get; set; }
    public bool HasDisasterReliefAgency { get; set; }
    public int SlotsUsed { get; set; }
    public int SlotsFull { get; set; }
    public int TotalCashSent { get; set; }
    public int TotalCashReceived { get; set; }
    public int TotalTechSent { get; set; }
    public int TotalTechReceived { get; set; }
    public int Credit { get; set; }
    public int Debt { get; set; }
    public int CashSentTechCredit { get; set; }
    public int CashReceivedTechCredit { get; set; }
    public int CashSentCashCredit { get; set; }
    public int CashReceivedCashCredit { get; set; }
    public int TechSentCashCredit { get; set; }
    public int TechReceivedCashCredit { get; set; }
    public int TechSentTechCredit { get; set; }
    public int TechReceivedTechCredit { get; set; }
    public int PreviousListOrder { get; set; }
    public int TotalTimesInDebt { get; set; }
    public decimal AvgDaysToClearDebt { get; set; }
    public int TotalTimesWithCredit { get; set; }
    public decimal AvgDaysToFillCredit { get; set; }
}