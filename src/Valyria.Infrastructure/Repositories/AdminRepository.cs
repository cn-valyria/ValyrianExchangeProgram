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

    public async Task<GeneralProgramSummary> GetGeneralProgramSummaryAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select
	(select count(1) from account where va <> 'H') as TotalAccounts,
    (select count(1) from deal where status = 'active') as ActiveDeals";

        return await sqlConnection.QuerySingleAsync<GeneralProgramSummary>(query);
    }

    public async Task<AidRatioSummary> GetAidRatioSummaryAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select
    coalesce(sum(slot.cash_received_cycles > 0), 0) as TotalIncomingCashSlots,
    coalesce(sum(slot.cash_sent_cycles > 0), 0) as TotalOutgoingCashSlots,
    coalesce(sum(slot.tech_received_cycles > 0), 0) as TotalIncomingTechSlots,
    coalesce(sum(slot.tech_sent_cycles > 0), 0) as TotalOutgoingTechSlots
from slot
join account on slot.account_id = account.id
where account.va <> 'H';";

        return await sqlConnection.QuerySingleAsync<AidRatioSummary>(query);
    }

    public async Task<DealHealthSummary> GetDealHealthSummaryAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select
    (select count(1) from wait_queue) as AccountsWaiting,
    (select coalesce(sum(slots_needed), 0) from wait_queue) as SlotsWaiting,
    (select count(1) from deal_flag where resolved_on is null) as OpenFlags;";

        return await sqlConnection.QuerySingleAsync<DealHealthSummary>(query);
    }

    public async Task<SlotUsageSummary> GetSlotUsageSummaryAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select coalesce(sum(slots_total), 0) as MaxCapacity, coalesce(sum(slots_used), 0) as CurrentSlotsUsed
from account_deal_summary
where role <> 'H';";

        return await sqlConnection.QuerySingleAsync<SlotUsageSummary>(query);
    }

    public async Task<List<DealAidCountByDay>> GetDealAidPerDayAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select activity_date as Day, count(1) as AidCount
from deal_activity
where activity_date > curdate() - interval 5 day
group by activity_date
order by activity_date;";

        return (await sqlConnection.QueryAsync<DealAidCountByDay>(query)).ToList();
    }

    public async Task<List<TotalSlotGroupsByAlliance>> GetTotalSlotsPerAllianceAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select
    alliance.name as AllianceName,
    coalesce(sum(slot.cash_received_cycles > 0), 0) as TotalIncomingCashSlots,
    coalesce(sum(slot.cash_sent_cycles > 0), 0) as TotalOutgoingCashSlots,
    coalesce(sum(slot.tech_received_cycles > 0), 0) as TotalIncomingTechSlots,
    coalesce(sum(slot.tech_sent_cycles > 0), 0) as TotalOutgoingTechSlots
from slot
join account on slot.account_id = account.id and account.va <> 'H'
join cybernations_db.nation on account.nation_id = nation.id
join cybernations_db.alliance on nation.alliance_id = alliance.id
group by alliance.name;";

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

    public async Task<List<AccountCountByRole>> GetAccountsPerRoleAsync()
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

    public async Task<List<OpenSlotCountByRole>> GetOpenSlotsPerRoleAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select role as Role, sum(slots_available) as OpenSlots
from account_deal_summary
where role <> 'H'
group by role
order by role;";

        return (await sqlConnection.QueryAsync<OpenSlotCountByRole>(query)).ToList();
    }

    public async Task<List<AccountCountByAlliance>> GetAccountsPerAllianceAsync()
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

    public async Task<List<AdminAccessSetting>> GetAdminAccessSettingsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select
    account.id as AccountId,
    ruler_name as RulerName,
    nation_name as NationName,
    account.va as VepRole,
    role.id as AdminRoleId,
    role.name as AdminRoleName
from account
join cybernations_db.nation on account.nation_id = nation.id
join account_role on account.id = account_role.account_id
join role on account_role.role_id = role.id;";

        return (await sqlConnection.QueryAsync<AdminAccessSetting>(query)).ToList();
    }

    public async Task<bool> CreateAdminAccessAsync(int accountId, int adminRoleId)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
insert into account_role (account_id, role_id)
values (@AccountId, @AdminRoleId);";

        var rowsAffected = await sqlConnection.ExecuteAsync(query, new { AccountId = accountId, AdminRoleId = adminRoleId });
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAdminAccessAsync(int accountId, int adminRoleId)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
delete from account_role
where account_id = @AccountId and role_id = @AdminRoleId;";

        var rowsAffected = await sqlConnection.ExecuteAsync(query, new { AccountId = accountId, AdminRoleId = adminRoleId });
        return rowsAffected > 0;
    }
}