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
using Valyria.Core.DTOs.CN;

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
        return (await sqlConnection.QueryAsync<SlotConfigSummary>(SlotConfigsQuery(byAlliance: false))).ToList();
    }

    public async Task<List<AllianceSlotConfigSummary>> GetAllianceSlotConfigsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        return (await sqlConnection.QueryAsync<AllianceSlotConfigSummary>(SlotConfigsQuery(byAlliance: true))).ToList();
    }

    // Accounts whose nation has no alliance are grouped as NoAlliance
    private const string NoAlliance = "No alliance";

    private static string SlotConfigsQuery(bool byAlliance)
    {
        var alliance = byAlliance ? $"coalesce(alliance.name, '{NoAlliance}')" : null;
        // Open slots on flagged accounts are counted apart, since populate_wait_queue leaves those accounts out
        return $@"
select
    {(byAlliance ? $"{alliance} as AllianceName, alliance_accounts.accounts as AllianceAccounts," : "")}
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
{(byAlliance ? $@"left join cybernations_db.nation on account.nation_id = nation.id
left join cybernations_db.alliance on nation.alliance_id = alliance.id
join (
    select coalesce(alliance.name, '{NoAlliance}') as alliance_name, count(1) as accounts
    from account
    left join cybernations_db.nation on account.nation_id = nation.id
    left join cybernations_db.alliance on nation.alliance_id = alliance.id
    where account.va <> 'H'
    group by alliance_name
) alliance_accounts on alliance_accounts.alliance_name = {alliance}" : "")}
left join (
    select slot_1_id as slot_id from deal where status = 'active'
    union
    select slot_2_id from deal where status = 'active'
) in_deal on in_deal.slot_id = slot.id
left join (
    select distinct flagged_account_id as account_id from deal_flag where resolved_on is null
) flagged on flagged.account_id = slot.account_id
-- A team bonus slot counts only while its event is in effect
left join team_bonus_slot on team_bonus_slot.slot_id = slot.id
where not slot.is_team_bonus or team_bonus_slot.slot_id is not null
group by {(byAlliance ? "AllianceName, AllianceAccounts, " : "")}slot.tech_sent_cycles, slot.tech_received_cycles, slot.cash_sent_cycles, slot.cash_received_cycles
order by {(byAlliance ? "AllianceName, " : "")}TotalSlots desc, TechSentCycles, TechReceivedCycles, CashSentCycles, CashReceivedCycles;";
    }

    public async Task<List<DealHealthByPairing>> GetDealHealthAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        return (await sqlConnection.QueryAsync<DealHealthByPairing>(DealHealthQuery(byAlliance: false))).ToList();
    }

    public async Task<List<AllianceDealHealth>> GetAllianceDealHealthAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        return (await sqlConnection.QueryAsync<AllianceDealHealth>(DealHealthQuery(byAlliance: true))).ToList();
    }

    private static string DealHealthQuery(bool byAlliance)
    {
        // Each deal counts once for each alliance it involves: once for a deal between two of an alliance's
        // own accounts (WithinAlliance), and once for each side of a deal between two alliances
        const string dealAlliances = $@"
join (
    select d.id as deal_id, coalesce(alliance.name, '{NoAlliance}') as alliance_name, count(1) = 2 as within_alliance
    from deal d
    join slot on slot.id in (d.slot_1_id, d.slot_2_id)
    join account on slot.account_id = account.id
    left join cybernations_db.nation on account.nation_id = nation.id
    left join cybernations_db.alliance on nation.alliance_id = alliance.id
    where d.status = 'active'
    group by d.id, alliance_name
) deal_alliance on deal_alliance.deal_id = d.id";

        // Health values mirror DealHealth. Deals without a deal_state row yet were created since the last
        // update_deal_state run. The 5 days mirror the replacement threshold in check_deal_inactivity
        return $@"
select
    {(byAlliance ? "deal_alliance.alliance_name as AllianceName, deal_alliance.within_alliance as WithinAlliance," : "")}
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
        when ds.state = 'pending_start' then if(datediff({CnToday}, ds.state_since) >= 5, 2, 1)
        when ds.state = 'blocked' or datediff({CnToday}, ds.responsible_since) >= 1 then 3
        else 0
    end as Health,
    count(1) as Deals
from deal d
join slot s1 on d.slot_1_id = s1.id
join slot s2 on d.slot_2_id = s2.id
left join deal_state ds on ds.deal_id = d.id
{(byAlliance ? dealAlliances : "")}
where d.status = 'active'
group by {(byAlliance ? "AllianceName, WithinAlliance, " : "")}Slot1TechSentCycles, Slot1TechReceivedCycles, Slot1CashSentCycles, Slot1CashReceivedCycles,
    Slot2TechSentCycles, Slot2TechReceivedCycles, Slot2CashSentCycles, Slot2CashReceivedCycles, Health
order by {(byAlliance ? "AllianceName, " : "")}Deals desc;";
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

    public async Task<List<TeamEvent>> GetTeamEventsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        // Bonus slots record the event they're in effect for, so counts are per event rather than per team
        const string query = @"
select
    team_event.id as Id,
    team_event.team as Team,
    team_event.approved_on as ApprovedOn,
    team_event.ends_on as EndsOn,
    active_team_event.id is not null as IsActive,
    (select count(1) from team_bonus_slot where team_bonus_slot.team_event_id = team_event.id) as AccountsWithBonusSlot,
    (select count(distinct deal.id)
        from team_bonus_slot
        join deal on team_bonus_slot.slot_id in (deal.slot_1_id, deal.slot_2_id)
        where team_bonus_slot.team_event_id = team_event.id and deal.status = 'active') as ActiveBonusDeals,
    team_event.created_on as CreatedOn,
    team_event.created_by as CreatedBy
from team_event
left join active_team_event on active_team_event.id = team_event.id
where team_event.deleted_on is null
order by team_event.approved_on desc, team_event.id desc;";

        return (await sqlConnection.QueryAsync<TeamEvent>(query)).ToList();
    }

    public async Task<TeamEventCreateResult> CreateTeamEventAsync(TeamEventCreateRequest request, string createdBy)
    {
        // None (Grey) has no senate to pass events
        if (request.Team < Team.Aqua || request.Team > Team.Pink)
            return TeamEventCreateResult.InvalidTeam;

        using var sqlConnection = new MySqlConnection(_connectionString);
        await sqlConnection.OpenAsync();
        using var transaction = await sqlConnection.BeginTransactionAsync();

        var approvedOn = request.ApprovedOn.Date;
        var today = await sqlConnection.QuerySingleAsync<System.DateTime>($"select {CnToday}", transaction: transaction);
        if (approvedOn > today)
            return TeamEventCreateResult.ApprovedInFuture;

        // Locks the team's events so two admins can't add overlapping ones at once
        const string overlapQuery = @"
select count(1)
from team_event
where team = @Team
    and deleted_on is null
    and approved_on <= @ApprovedOn + interval 30 day
    and ends_on >= @ApprovedOn
for update;";

        var overlapping = await sqlConnection.QuerySingleAsync<int>(overlapQuery,
            new { Team = (int)request.Team, ApprovedOn = approvedOn }, transaction);
        if (overlapping > 0)
            return TeamEventCreateResult.Overlaps;

        await sqlConnection.ExecuteAsync(@"
insert into team_event (team, approved_on, created_by)
values (@Team, @ApprovedOn, @CreatedBy);",
            new { Team = (int)request.Team, ApprovedOn = approvedOn, CreatedBy = createdBy }, transaction);

        await transaction.CommitAsync();

        // Gives the team's accounts their bonus slot now, rather than at the next loader run
        await SyncTeamEventSlotsAsync(sqlConnection);

        return TeamEventCreateResult.Created;
    }

    public async Task<bool> DeleteTeamEventAsync(int teamEventId, string deletedBy)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        await sqlConnection.OpenAsync();

        var rowsAffected = await sqlConnection.ExecuteAsync(@"
update team_event
set deleted_on = current_timestamp,
    deleted_by = @DeletedBy
where id = @Id and deleted_on is null;",
            new { Id = teamEventId, DeletedBy = deletedBy });

        if (rowsAffected == 0)
            return false;

        // Cancels deals on the event's bonus slots straight away
        await SyncTeamEventSlotsAsync(sqlConnection);

        return true;
    }

    private static Task SyncTeamEventSlotsAsync(MySqlConnection sqlConnection)
    {
        var parameters = new DynamicParameters();
        parameters.Add("p_deals_cancelled", dbType: DbType.Int32, direction: ParameterDirection.Output);

        return sqlConnection.ExecuteAsync("sync_team_event_slots", parameters, commandType: CommandType.StoredProcedure);
    }
}