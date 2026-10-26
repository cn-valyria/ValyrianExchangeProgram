using System;
using System.Collections.Generic;
using Valyria.Core.Entities;

namespace Valyria.Core.DTOs;

public class AdminDealItem
{
    public int Id { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? LastActivityDate { get; set; }
    /// <summary>
    /// Null until the data loader's next run picks up a newly created deal
    /// </summary>
    public DealActivityState? State { get; set; }
    public DealBlockedReason? BlockedReason { get; set; }
    public DateTime? StateSince { get; set; }
    public int? NextSenderAccountId { get; set; }
    /// <summary>
    /// Account holding up the deal, if any
    /// </summary>
    public int? ResponsibleAccountId { get; set; }
    public int? DaysResponsible { get; set; }
    public DateTime? CancelsOn { get; set; }
    public DateTime? InFlightUntil { get; set; }
    public bool HasOpenFlag { get; set; }
    public SlotDetail Slot1 { get; set; }
    public SlotDetail Slot2 { get; set; }
}

public class AdminDealPage
{
    public int TotalCount { get; set; }
    public List<AdminDealItem> Results { get; set; } = new();
}

/// <summary>
/// One side of an aid record. AccountId and Role are null when the nation isn't a VEP account.
/// </summary>
public class AidParty : NationSimplified
{
    public int? AccountId { get; set; }
    public string Role { get; set; }
}

public class AidActivityItem
{
    public int AidId { get; set; }
    public DateTime? SentOn { get; set; }
    public int Status { get; set; }
    public int Money { get; set; }
    public int Technology { get; set; }
    public int Soldiers { get; set; }
    public string Reason { get; set; }
    /// <summary>
    /// The deal this aid was matched to, or null if it didn't match any deal
    /// </summary>
    public int? DealId { get; set; }
    public AidParty Sender { get; set; }
    public AidParty Receiver { get; set; }
}

public class AidActivityPage
{
    public int TotalCount { get; set; }
    public List<AidActivityItem> Results { get; set; } = new();
}

/// <summary>
/// The aid behind a deal flag
/// </summary>
public class DealFlagEvidence
{
    /// <summary>
    /// Every leg of the flag's deal, oldest first
    /// </summary>
    public List<FlagDealLeg> Legs { get; set; } = new();
    /// <summary>
    /// CN date the slot aid is as of: today while the deal is active, otherwise the day the flag was raised
    /// </summary>
    public DateTime SlotsAsOf { get; set; }
    public int FlaggedSlotsTotal { get; set; }
    /// <summary>
    /// Aid sent or received by the flagged nation that held one of its in-game aid slots as of SlotsAsOf, oldest first
    /// </summary>
    public List<FlagSlotAid> FlaggedSlotAid { get; set; } = new();
}

public class FlagDealLeg
{
    public int LegNumber { get; set; }
    public int? AidId { get; set; }
    public DateTime ActivityDate { get; set; }
    public bool SentByFlaggedAccount { get; set; }
    public int Money { get; set; }
    public int Technology { get; set; }
    public DealLegOutcome Outcome { get; set; }
    /// <summary>
    /// The leg failed, but an admin restored the deal it cancelled
    /// </summary>
    public bool FailureExcused { get; set; }
}

public class FlagSlotAid
{
    public int AidId { get; set; }
    public DateTime SentOn { get; set; }
    /// <summary>
    /// True if the flagged nation sent it, false if it received it
    /// </summary>
    public bool IsOutgoing { get; set; }
    public int Status { get; set; }
    public int Money { get; set; }
    public int Technology { get; set; }
    public int Soldiers { get; set; }
    public string Reason { get; set; }
    /// <summary>
    /// The deal this aid is a leg of, if any
    /// </summary>
    public int? DealId { get; set; }
    /// <summary>
    /// Sent before the flag's deal was created, so VEP doesn't hold it against the nation
    /// </summary>
    public bool SentBeforeDeal { get; set; }
    /// <summary>
    /// The nation on the other side. Names are null if the nation has since been deleted
    /// </summary>
    public NationSimplified OtherNation { get; set; }
}

public class AidActivityFilters
{
    /// <summary>
    /// Matches ruler, nation, or alliance name
    /// </summary>
    public string? Search { get; set; }
    /// <summary>
    /// Which side of the aid the search applies to: "sent", "received", or null for either
    /// </summary>
    public string? Direction { get; set; }
    public DateTime? SentSince { get; set; }
    public DateTime? SentUntil { get; set; }
    public bool IncludeOnHold { get; set; }
    public bool UnmatchedOnly { get; set; }
}
