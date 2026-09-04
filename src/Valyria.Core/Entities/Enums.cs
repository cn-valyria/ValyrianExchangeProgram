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

public enum DealDirection
{
    Bidirectional,
    Unidirectional
}

public enum DealStatus
{
    Active,
    Cancelled
}

public enum DealFlagType
{
    OneDayInactive,
    FiveDayReplacement
}