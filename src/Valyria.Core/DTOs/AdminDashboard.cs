using System;
using System.Collections.Generic;

namespace Valyria.Core.DTOs;

public class AdminDashboard
{
    public int TotalAccounts { get; set; }
    public int TotalBuyers { get; set; }
    public int TotalSellers { get; set; }
    public int TotalTransactions { get; set; }
    public int UnusedCredit { get; set; }

    public List<TransactionCountByDay> TransactionsPerDay { get; set; }
    public List<CreditGroupsByAlliance> PendingCreditByAlliance { get; set; }
    public List<AccountCountByRole> AccountsPerRole { get; set; }
    public List<TotalSlotCountByRole> TotalSlotsPerRole { get; set; }
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