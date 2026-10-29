using System;
using System.Collections.Generic;
using Valyria.Core.Entities;

namespace Valyria.Core.DTOs;

public class SlotDetail
{
    public int Id { get; set; }
    public byte SlotNumber { get; set; }
    public int TechSentCycles { get; set; }
    public int TechReceivedCycles { get; set; }
    public int CashSentCycles { get; set; }
    public int CashReceivedCycles { get; set; }
    public AccountSimplified Account { get; set; }
    public bool HasActiveDeal { get; set; }
    /// <summary>
    /// The extra slot an account has while its team's +1 aid slot event is in effect
    /// </summary>
    public bool IsTeamBonus { get; set; }
    /// <summary>
    /// Last CN date the team bonus slot is in effect. Null if it isn't a bonus slot, or the bonus has ended
    /// </summary>
    public DateTime? TeamBonusEndsOn { get; set; }
    /// <summary>
    /// Last CN date VEP matches a new deal into the team bonus slot
    /// </summary>
    public DateTime? TeamBonusMatchableUntil { get; set; }

    /// <summary>
    /// A team bonus slot whose event has ended, or whose nation has left the team. It can't be used until
    /// the account gets a bonus again
    /// </summary>
    public bool IsInactiveTeamBonus => IsTeamBonus && TeamBonusEndsOn == null;
}

public class SlotUpdateRequest
{
    public int SlotId { get; set; }
    public int TechSentCycles { get; set; }
    public int TechReceivedCycles { get; set; }
    public int CashSentCycles { get; set; }
    public int CashReceivedCycles { get; set; }
}

public class AccountSlotsResponse
{
    public int AccountId { get; set; }
    public AccountSimplified Account { get; set; }
    public List<SlotDetail> Slots { get; set; } = new();
}
