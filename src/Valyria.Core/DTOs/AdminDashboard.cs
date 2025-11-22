using System;
using System.Collections.Generic;

namespace Valyria.Core.DTOs;

public class AdminDashboard
{
    public int TotalAccounts { get; set; }
    public int TotalIncomingCashSlots { get; set; }
    public int TotalOutgoingCashSlots { get; set; }
    public int TotalIncomingTechSlots { get; set; }
    public int TotalOutgoingTechSlots { get; set; }
    public int TotalTransactions { get; set; }

    public List<TransactionCountByDay> TransactionsPerDay { get; set; }
    public List<CreditGroupsByAlliance> PendingCreditByAlliance { get; set; }
    public List<AccountCountByRole> AccountsPerRole { get; set; }
    public List<TotalSlotGroupsByAlliance> TotalSlotsPerAlliance { get; set; }
    public List<FreeSlotCountByList> FreeSlotsPerList { get; set; }
    public List<AccountCountByAlliance> AccountsPerAlliance { get; set; }
}

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