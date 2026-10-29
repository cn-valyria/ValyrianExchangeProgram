using System.Collections.Generic;

namespace Valyria.Core.DTOs;

public class AccountDealsSummary
{
    public int SlotsUsed { get; set; }
    public int SlotsTotal { get; set; }
    public int SlotsAvailable { get; set; }
    public bool SlotsFull { get; set; }
    /// <summary>
    /// The team event giving the account a bonus slot, if one is in effect
    /// </summary>
    public AccountTeamEvent? TeamEvent { get; set; }
    public List<DealDetail> ActiveDeals { get; set; } = new();
    public List<DealNotification> Notifications { get; set; } = new();
}

public class DealNotification
{
    public string Type { get; set; } // "warning", "error", "info"
    public string Message { get; set; }
    public int? DealId { get; set; }
}
