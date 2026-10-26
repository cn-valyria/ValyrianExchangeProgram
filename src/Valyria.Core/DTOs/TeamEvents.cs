using System;
using Valyria.Core.DTOs.CN;

namespace Valyria.Core.DTOs;

/// <summary>
/// A team's +1 aid slot event. Every account on the team has a bonus slot from ApprovedOn through EndsOn
/// </summary>
public class TeamEvent
{
    public int Id { get; set; }
    public Team Team { get; set; }
    /// <summary>
    /// CN date a majority of the team's senators passed the proposal
    /// </summary>
    public DateTime ApprovedOn { get; set; }
    /// <summary>
    /// Last CN date the event is in effect, 30 days after ApprovedOn
    /// </summary>
    public DateTime EndsOn { get; set; }
    public bool IsActive { get; set; }
    /// <summary>
    /// Accounts currently on the team that have the bonus slot. 0 once the event has ended
    /// </summary>
    public int AccountsWithBonusSlot { get; set; }
    /// <summary>
    /// Active deals using one of the event's bonus slots
    /// </summary>
    public int ActiveBonusDeals { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; }
}

public class TeamEventCreateRequest
{
    public Team Team { get; set; }
    public DateTime ApprovedOn { get; set; }
}

/// <summary>
/// The team event an account's bonus slot comes from
/// </summary>
public class AccountTeamEvent
{
    public Team Team { get; set; }
    public DateTime ApprovedOn { get; set; }
    public DateTime EndsOn { get; set; }
    /// <summary>
    /// Last CN date VEP matches a new deal into the bonus slot. Before EndsOn when the slot's deals need more
    /// than one leg, so every leg can be sent before the event ends
    /// </summary>
    public DateTime BonusSlotMatchableUntil { get; set; }
}

public enum TeamEventCreateResult
{
    Created,
    /// <summary>The team is None (Grey), which has no senate, or isn't a CN team</summary>
    InvalidTeam,
    /// <summary>The approval date is after today's CN date</summary>
    ApprovedInFuture,
    /// <summary>Another event for the same team overlaps this one</summary>
    Overlaps
}
