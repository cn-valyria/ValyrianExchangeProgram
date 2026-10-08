namespace Valyria.Core.DTOs;

public class DealAidCountByDay
{
    public DateTime Day { get; set; }
    public int AidCount { get; set; }
}

public class TotalSlotGroupsByAlliance
{
    public string SlotGroupName { get; set; }
    public List<TotalSlotsGroup> TotalSlotGroups { get; set; }
}

public class TotalSlotsGroup
{
    public string AllianceName { get; set; }
    public int TotalSlots { get; set; }
}

public class AccountCountByRole
{
    public char Role { get; set; }
    public int TotalAccounts { get; set; }
}

public class OpenSlotCountByRole
{
    public char Role { get; set; }
    public int OpenSlots { get; set; }
}

public class AccountCountByAlliance
{
    public string AllianceName { get; set; }
    public int TotalAccounts { get; set; }
}

public class GeneralProgramSummary
{
    public int TotalAccounts { get; set; }
    public int ActiveDeals { get; set; }
}

/// <summary>
/// Number of slots that send or receive each kind of aid
/// </summary>
public class AidRatioSummary
{
    public int TotalIncomingCashSlots { get; set; }
    public int TotalOutgoingCashSlots { get; set; }
    public int TotalIncomingTechSlots { get; set; }
    public int TotalOutgoingTechSlots { get; set; }
}

public class DealHealthSummary
{
    public int AccountsWaiting { get; set; }
    public int SlotsWaiting { get; set; }
    public int OpenFlags { get; set; }
}

public class SlotUsageSummary
{
    public int MaxCapacity { get; set; }
    public int CurrentSlotsUsed { get; set; }
}
