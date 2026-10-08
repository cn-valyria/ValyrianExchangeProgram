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
    /// Days since the last activity, or since the deal was created if there has been no activity
    /// </summary>
    public int DaysSinceActivity { get; set; }
    public bool NeedsAction { get; set; }
    public string ActionNeeded { get; set; }
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
}
