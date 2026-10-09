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
