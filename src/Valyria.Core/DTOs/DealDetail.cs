using System;
using System.Collections.Generic;
using Valyria.Core.Entities;

namespace Valyria.Core.DTOs;

public class DealDetail
{
    public int Id { get; set; }
    public DateTime CreatedOn { get; set; }
    public SlotDetail MySlot { get; set; }
    public SlotDetail PartnerSlot { get; set; }
    public string PartnerDiscord { get; set; }
    /// <summary>
    /// Date of the most recent aid tied to this deal, or null if none has been sent yet
    /// </summary>
    public DateTime? LastActivityDate { get; set; }
    /// <summary>
    /// Null until the data loader's next run picks up a newly created deal
    /// </summary>
    public DealActivityState? State { get; set; }
    public DealBlockedReason? BlockedReason { get; set; }
    public DateTime? StateSince { get; set; }
    /// <summary>
    /// True if the requesting account sends the next leg (after any in-flight leg expires)
    /// </summary>
    public bool IsMyTurnToSend { get; set; }
    /// <summary>
    /// True if the requesting account is holding up the deal
    /// </summary>
    public bool IAmResponsible { get; set; }
    /// <summary>
    /// True if the partner is holding up the deal
    /// </summary>
    public bool PartnerIsResponsible { get; set; }
    /// <summary>
    /// Days the responsible account has held up the deal
    /// </summary>
    public int? DaysResponsible { get; set; }
    /// <summary>
    /// CN date the deal is cancelled if the responsible account is still holding it up
    /// </summary>
    public DateTime? CancelsOn { get; set; }
    /// <summary>
    /// First CN date the in-flight leg no longer occupies a slot
    /// </summary>
    public DateTime? InFlightUntil { get; set; }
    /// <summary>
    /// True if the in-flight leg is coming to the requesting account
    /// </summary>
    public bool InFlightIsIncoming { get; set; }
    /// <summary>
    /// True once the in-flight leg's offer has been accepted
    /// </summary>
    public bool InFlightAccepted { get; set; }
    /// <summary>
    /// First CN date the out-of-sequence aid blocking the deal no longer occupies a slot
    /// </summary>
    public DateTime? BlockedUntil { get; set; }
    /// <summary>
    /// When the requesting account was warned for holding up this deal, if the warning is still open
    /// </summary>
    public DateTime? FlaggedOn { get; set; }
}

public class DealHistoryItem
{
    public int Id { get; set; }
    public DealStatus Status { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? CancelledOn { get; set; }
    public string CancellationReason { get; set; }
    public byte MySlotNumber { get; set; }
    public int AidSentCount { get; set; }
    public int AidReceivedCount { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public AccountSimplified Partner { get; set; }
}

public class DealHistoryPage
{
    public int TotalCount { get; set; }
    public List<DealHistoryItem> Results { get; set; } = new();
}

public class DealActivityDetail
{
    public int Id { get; set; }
    public int? AidId { get; set; }
    public DateTime ActivityDate { get; set; }
    /// <summary>
    /// True if the requesting account sent this aid, false if it received it
    /// </summary>
    public bool IsOutgoing { get; set; }
    public int Money { get; set; }
    public int Technology { get; set; }
    public DealLegOutcome Outcome { get; set; }
}
