using System;
using System.Collections.Generic;
using Valyria.Core.Entities;

namespace Valyria.Core.DTOs;

/// <summary>
/// A daily mass message, with today's recipients and whether an admin has sent it yet
/// </summary>
public class MassMessage
{
    public int Id { get; set; }
    public MassMessageCode Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    /// <summary>
    /// {{date}} and {{siteUrl}} are filled in by the UI
    /// </summary>
    public string Subject { get; set; }
    public string Message { get; set; }
    public DateTime? SentOn { get; set; }
    /// <summary>
    /// Null until an admin marks today's messages as sent
    /// </summary>
    public AccountSimplified? SentBy { get; set; }
    public List<MassMessageRecipient> Recipients { get; set; } = new();
}

/// <summary>
/// An account that needs to act on its deals today. The counts are for admins only;
/// messages never include deal-specific information
/// </summary>
public class MassMessageRecipient
{
    public int AccountId { get; set; }
    public int NationId { get; set; }
    public string RulerName { get; set; }
    public string NationName { get; set; }
    public string AllianceName { get; set; }
    public string Discord { get; set; }
    /// <summary>
    /// Used by the data loader to ping the recipient on Discord. 0 if the account has no Discord ID
    /// </summary>
    public long DiscordUniqueId { get; set; }
    public int SendCount { get; set; }
    public int AcceptCount { get; set; }
    /// <summary>
    /// Deals blocked by this account's full slots or out-of-turn aid
    /// </summary>
    public int BlockedCount { get; set; }
    /// <summary>
    /// Deals with an open warning against this account that it's still holding up
    /// </summary>
    public int FlaggedCount { get; set; }
    /// <summary>
    /// Longest any flagged deal has been held up by this account, or null if none are flagged
    /// </summary>
    public int? DaysOverdue { get; set; }
    /// <summary>
    /// When the soonest flagged deal is cancelled, or null if none are flagged
    /// </summary>
    public DateTime? CancelsOn { get; set; }
}

/// <summary>
/// The mass messages for today's CN date
/// </summary>
public class MassMessagesSummary
{
    public DateTime CnDate { get; set; }
    /// <summary>
    /// Last time the data loader changed any deal's state
    /// </summary>
    public DateTime? DealStatesUpdatedOn { get; set; }
    public List<MassMessage> Messages { get; set; } = new();
}

public class MassMessageSentRequest
{
    public bool Sent { get; set; }
}
