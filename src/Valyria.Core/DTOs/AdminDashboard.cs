namespace Valyria.Core.DTOs;

public class TransactionCountByDay
{
    public DateTime DayTxnRecorded { get; set; }
    public int TxnCount { get; set; }
}

public class CreditGroupsByAlliance
{
    public string AllianceName { get; set; }
    public List<CreditGroup> CreditGroups { get; set; }
}

public class CreditGroup
{
    public string DateRange { get; set; }
    public int TotalCredit { get; set; }
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

public class TotalSlotCountByRole
{
    public char Role { get; set; }
    public int TotalSlots { get; set; }
}

public class FreeSlotCountByList
{
    public string ListName { get; set; }
    public int FreeSlots { get; set; }
}

public class AccountCountByAlliance
{
    public string AllianceName { get; set; }
    public int TotalAccounts { get; set; }
}

public class GeneralProgramSummary
{
    public int TotalAccounts { get; set; }
    public int TotalTransactions { get; set; }
}

public class AidRatioSummary
{
    public int TotalIncomingCashSlots { get; set; }
    public int TotalOutgoingCashSlots { get; set; }
    public int TotalIncomingTechSlots { get; set; }
    public int TotalOutgoingTechSlots { get; set; }
}

public class UnusedCreditSummary
{
    public int UnusedCredit { get; set; }
}

public class SlotUsageSummary
{
    public int MaxCapacity { get; set; }
    public int CurrentSlotsUsed { get; set; }
}