using System;
using System.Collections.Generic;

namespace Valyria.Core.DTOs;

/// <summary>
/// Accounts ranked by unexcused flags per outgoing leg over the last <see cref="WindowDays"/> CN days
/// </summary>
public class Leaderboard
{
    public int WindowDays { get; set; }

    /// <summary>
    /// The outgoing legs an account needs in the window to be ranked
    /// </summary>
    public int MinimumOutgoingLegs { get; set; }

    /// <summary>
    /// The first CN date in the window
    /// </summary>
    public DateTime WindowStart { get; set; }

    public int RankedAccounts { get; set; }

    /// <summary>
    /// The top ranks, in order. Exact ties share a rank, so this can hold more than the top 10 accounts
    /// </summary>
    public List<LeaderboardStanding> Top { get; set; } = [];

    /// <summary>
    /// The requesting account's standing. Null if the account is on hold
    /// </summary>
    public LeaderboardStanding? Mine { get; set; }
}

public class LeaderboardStanding
{
    public int AccountId { get; set; }
    public int NationId { get; set; }
    public string NationName { get; set; }
    public string RulerName { get; set; }
    public string AllianceName { get; set; }

    /// <summary>
    /// Legs the account sent in the window, not counting failed offers unless an admin excused them
    /// </summary>
    public int OutgoingLegs { get; set; }

    /// <summary>
    /// Unexcused flags raised against the account in the window
    /// </summary>
    public int Flags { get; set; }

    public decimal FlagsPerLeg => OutgoingLegs == 0 ? 0 : Math.Round((decimal)Flags / OutgoingLegs, 4);

    /// <summary>
    /// Null when the account doesn't have enough outgoing legs to be ranked
    /// </summary>
    public int? Rank { get; set; }

    public int LegsToQualify { get; set; }

    /// <summary>
    /// The CN date the account's oldest flag in the window stops counting
    /// </summary>
    public DateTime? NextFlagDropsOn { get; set; }
}
