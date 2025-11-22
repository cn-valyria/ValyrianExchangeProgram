using Valyria.Core.Interfaces;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Valyria.Core.DTOs;

namespace Valyria.Infrastructure.Repositories;

public class AdminRepository : IAdminRepository
{
    private readonly string _connectionString;

    public AdminRepository(string connectionString) => _connectionString = connectionString;

    public async Task<AdminDashboard> GetAdminDashboardAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select
	(select count(1) from account where va <> 'H') as TotalAccounts,
	coalesce(sum(case when va in ('S', 'N') then slots * multiplier else 0 end), 0) + 
        coalesce(sum(case when va = 'C' then slots * multiplier else 0 end), 0) as TotalIncomingCashSlots,
    coalesce(sum(case when va = 'B' then slots * multiplier else 0 end), 0) + 
        coalesce(sum(case when va = 'D' then slots * multiplier else 0 end), 0) as TotalOutgoingCashSlots,
    coalesce(sum(case when va = 'B' then slots * multiplier else 0 end), 0) + 
        coalesce(sum(case when va = 'R' then slots * multiplier else 0 end), 0) as TotalIncomingTechSlots,
    coalesce(sum(case when va in ('S', 'N') then slots * multiplier else 0 end), 0) + 
        coalesce(sum(case when va = 'F' then slots * multiplier else 0 end), 0) as TotalOutgoingTechSlots,
    (select count(1) from transaction) as TotalTransactions
from (
	select 
		va,
		sum(aid_activity.slots_full) as slots,
		case when va in ('F', 'D', 'C', 'R') then 2 else 1 end as multiplier
	from account
	join aid_activity on account.id = aid_activity.account_id and account.va <> 'H'
	group by va
) root;";

        var dashboard = await sqlConnection.QuerySingleAsync<AdminDashboard>(query);

        var transactionsPerDayTask = GetTransactionsPerDayAsync();
        var pendingCreditPerDayTask = GetPendingCreditPerAllianceAsync();
        var accountsPerRoleTask = GetAccountsPerRoleAsync();
        var totalSlotsPerAlliance = GetTotalSlotsPerAllianceAsync();
        var freeSlotsPerListTask = GetFreeSlotsPerListAsync();
        var accountsPerAllianceTask = GetAccountsPerAllianceAsync();

        await Task.WhenAll(transactionsPerDayTask, pendingCreditPerDayTask, accountsPerRoleTask, totalSlotsPerAlliance, freeSlotsPerListTask, accountsPerAllianceTask);

        dashboard.TransactionsPerDay = transactionsPerDayTask.Result;
        dashboard.PendingCreditByAlliance = pendingCreditPerDayTask.Result;
        dashboard.AccountsPerRole = accountsPerRoleTask.Result;
        dashboard.TotalSlotsPerAlliance = totalSlotsPerAlliance.Result;
        dashboard.FreeSlotsPerList = freeSlotsPerListTask.Result;
        dashboard.AccountsPerAlliance = accountsPerAllianceTask.Result;

        return dashboard;
    }

    private async Task<List<TransactionCountByDay>> GetTransactionsPerDayAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select date(`date`) as DayTxnRecorded, count(1) as TxnCount
from transaction
join cybernations_db.aid on transaction.aid_id = aid.id
join account sending_account on aid.sending_nation_id = sending_account.nation_id
join account receiving_account on aid.receiving_nation_id = receiving_account.nation_id
where `date` > curdate() - interval 5 day
and sending_account.va <> 'H' and receiving_account.va <> 'H'
group by date(`date`);";

        return (await sqlConnection.QueryAsync<TransactionCountByDay>(query)).ToList();
    }
    
    private async Task<List<CreditGroupsByAlliance>> GetPendingCreditPerAllianceAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);

        var results = new List<CreditGroupsByAlliance>();
        var rawData = await sqlConnection.QueryAsync<PendingCreditRawData>("get_pending_credit_per_alliance", CommandType.StoredProcedure);
        foreach (var grouping in rawData.GroupBy(x => x.AllianceName))
        {
            var newGroup = new CreditGroupsByAlliance
            {
                AllianceName = grouping.Key,
                CreditGroups = grouping.Select(data => new CreditGroup { DateRange = data.DateRange, TotalCredit = data.TotalCredit }).ToList()
            };

            results.Add(newGroup);
        }

        return results;
    }

    private class PendingCreditRawData
    {
        public string DateRange { get; set; }
        public string AllianceName { get; set; }
        public int TotalCredit { get; set; }
    }

    private async Task<List<TotalSlotGroupsByAlliance>> GetTotalSlotsPerAllianceAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select
	name as AllianceName,
	coalesce(sum(case when va in ('S', 'N') then slots * multiplier else 0 end), 0) + 
        coalesce(sum(case when va = 'C' then slots * multiplier else 0 end), 0) as TotalIncomingCashSlots,
    coalesce(sum(case when va = 'B' then slots * multiplier else 0 end), 0) + 
        coalesce(sum(case when va = 'D' then slots * multiplier else 0 end), 0) as TotalOutgoingCashSlots,
    coalesce(sum(case when va = 'B' then slots * multiplier else 0 end), 0) + 
        coalesce(sum(case when va = 'R' then slots * multiplier else 0 end), 0) as TotalIncomingTechSlots,
    coalesce(sum(case when va in ('S', 'N') then slots * multiplier else 0 end), 0) + 
        coalesce(sum(case when va = 'F' then slots * multiplier else 0 end), 0) as TotalOutgoingTechSlots
from (
	select 
		alliance.name,
		va,
		sum(aid_activity.slots_full) as slots,
		case when va in ('F', 'D', 'C', 'R') then 2 else 1 end as multiplier
	from account
	join aid_activity on account.id = aid_activity.account_id and account.va <> 'H'
    join cybernations_db.nation on account.nation_id = nation.id
    join cybernations_db.alliance on nation.alliance_id = alliance.id
	group by alliance.name, va
) root
group by name;";

        var results = new List<TotalSlotGroupsByAlliance>
        {
            new() { SlotGroupName = "Incoming Cash", TotalSlotGroups = [] },
            new() { SlotGroupName = "Outgoing Cash", TotalSlotGroups = [] },
            new() { SlotGroupName = "Incoming Tech", TotalSlotGroups = [] },
            new() { SlotGroupName = "Outgoing Tech", TotalSlotGroups = [] },
        };

        var rawData = await sqlConnection.QueryAsync<SlotGroupsRawData>(query);
        foreach (var alliance in rawData)
        {
            AddAllianceSlotsIfNotZero(results[0], alliance.AllianceName, alliance.TotalIncomingCashSlots);
            AddAllianceSlotsIfNotZero(results[1], alliance.AllianceName, alliance.TotalOutgoingCashSlots);
            AddAllianceSlotsIfNotZero(results[2], alliance.AllianceName, alliance.TotalIncomingTechSlots);
            AddAllianceSlotsIfNotZero(results[3], alliance.AllianceName, alliance.TotalOutgoingTechSlots);
        }

        return results;

        static void AddAllianceSlotsIfNotZero(TotalSlotGroupsByAlliance grouping, string allianceName, int slotCount)
        {
            if (slotCount > 0)
                grouping.TotalSlotGroups.Add(new() { AllianceName = allianceName, TotalSlots = slotCount });
        }
    }

    private class SlotGroupsRawData
    {
        public string AllianceName { get; set; }
        public int TotalIncomingCashSlots { get; set; }
        public int TotalOutgoingCashSlots { get; set; }
        public int TotalIncomingTechSlots { get; set; }
        public int TotalOutgoingTechSlots { get; set; }
    }

    private async Task<List<AccountCountByRole>> GetAccountsPerRoleAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select va as Role, count(1) as TotalAccounts
from account
join cybernations_db.nation on account.nation_id = nation.id
where va <> 'H'
group by va;";

        return (await sqlConnection.QueryAsync<AccountCountByRole>(query)).ToList();
    }

    private async Task<List<TotalSlotCountByRole>> GetTotalSlotsPerRoleAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select va as Role, sum(slots_full) as TotalSlots
from account
join aid_activity on account.id = aid_activity.account_id
where va <> 'H'
group by va;";

        return (await sqlConnection.QueryAsync<TotalSlotCountByRole>(query)).ToList();
    }

    private async Task<List<FreeSlotCountByList>> GetFreeSlotsPerListAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select list.name as ListName, sum(slots_full) - sum(slots_used) as FreeSlots
from list
join list_recipient on list.id = list_recipient.list_id
join aid_activity on list_recipient.account_id = aid_activity.account_id
group by list.name;";

        return (await sqlConnection.QueryAsync<FreeSlotCountByList>(query)).ToList();
    }

    private async Task<List<AccountCountByAlliance>> GetAccountsPerAllianceAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select alliance.name as AllianceName, count(1) as TotalAccounts
from account
join cybernations_db.nation on account.nation_id = nation.id
join cybernations_db.alliance on nation.alliance_id = alliance.id
where account.va <> 'H'
group by alliance.name
order by count(1) desc;";

        return (await sqlConnection.QueryAsync<AccountCountByAlliance>(query)).ToList();
    }
}