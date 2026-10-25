namespace Valyria.Core.DTOs;

/// <summary>
/// Program-wide counts. On-hold accounts are left out
/// </summary>
public class ProgramSummary
{
    public int TotalAccounts { get; set; }
    public int ActiveDeals { get; set; }
    public int OpenFlags { get; set; }
    /// <summary>Accounts with an unresolved flag, which keeps them out of matching</summary>
    public int FlaggedAccounts { get; set; }
}

/// <summary>
/// Every slot with the same cycle values, on accounts that aren't on hold. Matching pairs a slot only with
/// slots whose cycles are the exact inverse, so each distinct set of cycle values is its own pool
/// </summary>
public class SlotConfigSummary
{
    public int TechSentCycles { get; set; }
    public int TechReceivedCycles { get; set; }
    public int CashSentCycles { get; set; }
    public int CashReceivedCycles { get; set; }
    public int Accounts { get; set; }
    public int TotalSlots { get; set; }
    public int SlotsInDeals { get; set; }
    /// <summary>Slots not in a deal, on accounts matching can use (no unresolved flag)</summary>
    public int OpenSlots { get; set; }
    /// <summary>Accounts with at least one of <see cref="OpenSlots"/>. Two accounts can share only one deal</summary>
    public int OpenAccounts { get; set; }
    /// <summary>Slots not in a deal, on accounts kept out of matching by an unresolved flag</summary>
    public int FlaggedOpenSlots { get; set; }
}

/// <summary>
/// <see cref="SlotConfigSummary"/> for one alliance's accounts
/// </summary>
public class AllianceSlotConfigSummary : SlotConfigSummary
{
    public string AllianceName { get; set; }
    /// <summary>All of the alliance's accounts, whatever their slots. Accounts can have slots in several configs</summary>
    public int AllianceAccounts { get; set; }
}

/// <summary>
/// How an active deal is doing, from deal_state
/// </summary>
public enum DealHealth
{
    /// <summary>Aid is in flight, or the sender's turn started today</summary>
    OnTrack,
    /// <summary>New, or waiting for both nations to have a free aid slot (fewer than 5 days)</summary>
    Starting,
    /// <summary>Waiting for both nations to have a free aid slot for 5+ days. Nobody is at fault</summary>
    Stalled,
    /// <summary>Blocked, or an account has held it up for a day or more. Cancelled after 5 days</summary>
    AtRisk
}

/// <summary>
/// Active deals between slots with these cycle values, in one health bucket
/// </summary>
public class DealHealthByPairing
{
    public int Slot1TechSentCycles { get; set; }
    public int Slot1TechReceivedCycles { get; set; }
    public int Slot1CashSentCycles { get; set; }
    public int Slot1CashReceivedCycles { get; set; }
    public int Slot2TechSentCycles { get; set; }
    public int Slot2TechReceivedCycles { get; set; }
    public int Slot2CashSentCycles { get; set; }
    public int Slot2CashReceivedCycles { get; set; }
    public DealHealth Health { get; set; }
    public int Deals { get; set; }
}

/// <summary>
/// <see cref="DealHealthByPairing"/> for deals involving one alliance's accounts. A deal between two
/// alliances counts for both of them
/// </summary>
public class AllianceDealHealth : DealHealthByPairing
{
    public string AllianceName { get; set; }
    /// <summary>Both accounts in these deals are in this alliance</summary>
    public bool WithinAlliance { get; set; }
}

/// <summary>
/// Deal activity on one CN day
/// </summary>
public class DailyDealActivity
{
    public DateTime Day { get; set; }
    public int LegsSent { get; set; }
    public int LegsFailed { get; set; }
    public int DealsCreated { get; set; }
    public int DealsCancelled { get; set; }
}

/// <summary>
/// Deals cancelled for one reason. Admin cancellations share one reason, since admins type their own
/// </summary>
public class CancellationReasonCount
{
    public string Reason { get; set; }
    public int Deals { get; set; }
}
