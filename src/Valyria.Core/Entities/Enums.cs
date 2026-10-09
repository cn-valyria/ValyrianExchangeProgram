namespace Valyria.Core.Entities;

public enum Role
{
    NotParticipating = 0,
    Buyer,
    Seller,
    Donor,
    Farm,
    Collector,
    Receiver,
    ProbationarySeller,
    TemporaryDonor,
    TemporaryCollector,
    TemporaryFarm,
    TemporaryReceiver
}

public enum RecentActivity
{
    ActiveInTheLast3Days = 1,
    ActiveThisWeek = 2,
    ActiveLastWeek = 3,
    ActiveThreeWeeksAgo = 4,
    ActiveMoreThanThreeWeeksAgo = 5
}

public enum NationalWarStatus
{
    War = 1,
    Peace = 2
}

public enum AidStatus
{
    Pending = 1,
    Approved = 2,
    Cancled = 3, // Yes this is intentionally misspelled; admin is dumb and can't spell apparently
    Expired = 4
}

public enum DealStatus
{
    Active,
    Cancelled
}

public enum DealFlagType
{
    OneDayInactive,
    FiveDayReplacement,
    AidFailed
}

/// <summary>
/// Why a flagged account was responsible (deal_flag.flag_reason)
/// </summary>
public enum DealFlagReason
{
    AwaitingSend,
    SenderSlotsFull,
    ReceiverSlotsFull,
    OutOfSequenceAid,
    AidCancelled,
    AidExpired
}

/// <summary>
/// Where a deal is in its aid exchange (deal_state.state)
/// </summary>
public enum DealActivityState
{
    /// <summary>The pair hasn't been able to exchange aid yet. Nobody is at fault</summary>
    PendingStart,
    /// <summary>A counted leg is active between the pair</summary>
    InFlight,
    /// <summary>Both nations have a free slot; the next sender should send</summary>
    AwaitingSend,
    /// <summary>A nation's slots are full of other aid, or the wrong side sent aid</summary>
    Blocked
}

/// <summary>
/// Why a deal is blocked (deal_state.blocked_reason)
/// </summary>
public enum DealBlockedReason
{
    SenderSlotsFull,
    ReceiverSlotsFull,
    OutOfSequenceAid
}

/// <summary>
/// Which accounts a daily mass message goes to (mass_message.code). Each account gets the
/// most urgent message that applies to it
/// </summary>
public enum MassMessageCode
{
    /// <summary>Has aid to send or slots to free, and isn't flagged</summary>
    SendAid,
    /// <summary>Has an aid offer to accept, and isn't flagged</summary>
    AcceptAid,
    /// <summary>Flagged for holding up a deal</summary>
    Overdue,
    /// <summary>Flagged, and a deal is cancelled at the next daily check</summary>
    FinalWarning
}

/// <summary>
/// What happened to an aid offer counted as a deal leg
/// </summary>
public enum DealLegOutcome
{
    Pending,
    Accepted,
    Cancelled,
    Expired
}