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
    // VEP 3.0: Replaced balance/credit/debt with deals-based system
    // Slot information now comes from AccountDealsSummary
    // Historical transaction data still available via transaction history
}