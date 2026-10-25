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

    // The CN date the deal procedures use
    private const string CnToday = "DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '-05:00'))";

    public async Task<ProgramSummary> GetProgramSummaryAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select
    (select count(1) from account where va <> 'H') as TotalAccounts,
    (select count(1) from deal where status = 'active') as ActiveDeals,
    (select count(1) from deal_flag where resolved_on is null) as OpenFlags,
    (select count(distinct deal_flag.flagged_account_id)
        from deal_flag
        join account on deal_flag.flagged_account_id = account.id
        where deal_flag.resolved_on is null and account.va <> 'H') as FlaggedAccounts;";

        return await sqlConnection.QuerySingleAsync<ProgramSummary>(query);
    }

    public async Task<List<SlotConfigSummary>> GetSlotConfigsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        // Open slots on flagged accounts are counted apart, since populate_wait_queue leaves those accounts out
        const string query = @"
select
    slot.tech_sent_cycles as TechSentCycles,
    slot.tech_received_cycles as TechReceivedCycles,
    slot.cash_sent_cycles as CashSentCycles,
    slot.cash_received_cycles as CashReceivedCycles,
    count(distinct slot.account_id) as Accounts,
    count(1) as TotalSlots,
    coalesce(sum(in_deal.slot_id is not null), 0) as SlotsInDeals,
    coalesce(sum(in_deal.slot_id is null and flagged.account_id is null), 0) as OpenSlots,
    count(distinct case when in_deal.slot_id is null and flagged.account_id is null then slot.account_id end) as OpenAccounts,
    coalesce(sum(in_deal.slot_id is null and flagged.account_id is not null), 0) as FlaggedOpenSlots
from slot
join account on slot.account_id = account.id and account.va <> 'H'
left join (
    select slot_1_id as slot_id from deal where status = 'active'
    union
    select slot_2_id from deal where status = 'active'
) in_deal on in_deal.slot_id = slot.id
left join (
    select distinct flagged_account_id as account_id from deal_flag where resolved_on is null
) flagged on flagged.account_id = slot.account_id
group by slot.tech_sent_cycles, slot.tech_received_cycles, slot.cash_sent_cycles, slot.cash_received_cycles
order by TotalSlots desc, TechSentCycles, TechReceivedCycles, CashSentCycles, CashReceivedCycles;";

        return (await sqlConnection.QueryAsync<SlotConfigSummary>(query)).ToList();
    }

    public async Task<List<DealHealthByPairing>> GetDealHealthAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        // Health values mirror DealHealth. Deals without a deal_state row yet were created since the last
        // update_deal_state run. The 5 days mirror the replacement threshold in check_deal_inactivity
        const string query = @"
select
    s1.tech_sent_cycles as Slot1TechSentCycles,
    s1.tech_received_cycles as Slot1TechReceivedCycles,
    s1.cash_sent_cycles as Slot1CashSentCycles,
    s1.cash_received_cycles as Slot1CashReceivedCycles,
    s2.tech_sent_cycles as Slot2TechSentCycles,
    s2.tech_received_cycles as Slot2TechReceivedCycles,
    s2.cash_sent_cycles as Slot2CashSentCycles,
    s2.cash_received_cycles as Slot2CashReceivedCycles,
    case
        when ds.deal_id is null then 1
        when ds.state = 'pending_start' then if(datediff(" + CnToday + @", ds.state_since) >= 5, 2, 1)
        when ds.state = 'blocked' or datediff(" + CnToday + @", ds.responsible_since) >= 1 then 3
        else 0
    end as Health,
    count(1) as Deals
from deal d
join slot s1 on d.slot_1_id = s1.id
join slot s2 on d.slot_2_id = s2.id
left join deal_state ds on ds.deal_id = d.id
where d.status = 'active'
group by Slot1TechSentCycles, Slot1TechReceivedCycles, Slot1CashSentCycles, Slot1CashReceivedCycles,
    Slot2TechSentCycles, Slot2TechReceivedCycles, Slot2CashSentCycles, Slot2CashReceivedCycles, Health
order by Deals desc;";

        return (await sqlConnection.QueryAsync<DealHealthByPairing>(query)).ToList();
    }

    public async Task<List<DailyDealActivity>> GetDealActivityAsync(int days)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        // One row per CN day, ending today, including days with no activity
        const string query = @"
with recursive cn_day as (
    select " + CnToday + @" - interval (@Days - 1) day as cn_date
    union all
    select cn_date + interval 1 day from cn_day where cn_date < " + CnToday + @"
)
select
    cn_day.cn_date as Day,
    (select count(1) from deal_activity where activity_date = cn_day.cn_date) as LegsSent,
    (select count(1) from deal_activity where failed_on = cn_day.cn_date) as LegsFailed,
    (select count(1) from deal where date(convert_tz(created_on, '+00:00', '-05:00')) = cn_day.cn_date) as DealsCreated,
    (select count(1) from deal where date(convert_tz(cancelled_on, '+00:00', '-05:00')) = cn_day.cn_date) as DealsCancelled
from cn_day
order by cn_day.cn_date;";

        return (await sqlConnection.QueryAsync<DailyDealActivity>(query, new { Days = days })).ToList();
    }

    public async Task<List<CancellationReasonCount>> GetCancellationReasonsAsync(int days)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select
    if(cancelled_by = 'system', coalesce(cancellation_reason, 'No reason given'), 'Cancelled by an admin') as Reason,
    count(1) as Deals
from deal
where status = 'cancelled'
    and date(convert_tz(cancelled_on, '+00:00', '-05:00')) > " + CnToday + @" - interval @Days day
group by Reason
order by Deals desc, Reason;";

        return (await sqlConnection.QueryAsync<CancellationReasonCount>(query, new { Days = days })).ToList();
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