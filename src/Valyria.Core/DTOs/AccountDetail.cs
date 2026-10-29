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
    // Set by the member from their account page (see AccountSettings)
    public bool DiscordNotificationsEnabled { get; set; }
    public string UniqueCode { get; set; }
    public char Role { get; set; }
    public RecentActivity Activity { get; set; }
    public decimal Strength { get; set; }
    public decimal Infra { get; set; }
    public decimal Tech { get; set; }
    public NationalWarStatus WarStatus { get; set; }
    // The nation was deleted from CN. The nation fields are from its last known state
    public bool NationDeleted { get; set; }
    public bool HasForeignMinistry { get; set; }
    public bool HasFederalAidCommission { get; set; }
    public bool HasDisasterReliefAgency { get; set; }
    // VEP 3.0: Replaced balance/credit/debt with deals-based system.
    // Full deal details come from AccountDealsSummary; these are summary counts for admin lists.
    public int SlotsUsed { get; set; }
    public int SlotsTotal { get; set; }
    public int OpenFlagCount { get; set; }
}