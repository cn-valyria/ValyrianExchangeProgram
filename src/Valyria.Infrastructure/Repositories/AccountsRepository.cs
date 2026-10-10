using Valyria.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;
using Valyria.Core.DTOs;
using Valyria.Infrastructure.Exceptions;

namespace Valyria.Infrastructure.Repositories;

public class AccountsRepository : IAccountsRepository
{
    private readonly string _connectionString;
    
    private const string AccountDetailQuery = @"
select 	account.id as Id,
    nation.id as NationId,
    nation.nation_name as NationName,
    nation.ruler_name as RulerName,
    account.discord as Discord,
    account.discord_id as DiscordUniqueId,
    account.psw as UniqueCode,
    account.va as Role,
    alliance.name as AllianceName,
    nation.recent_activity as Activity,
    nation.strength as Strength,
    nation.infrastructure as Infra,
    nation.technology as Tech,
    nation.war_status as WarStatus,
    account.fm as HasForeignMinistry,
    account.fac as HasFederalAidCommission,
    account.dra as HasDisasterReliefAgency,
    coalesce(deal_summary.slots_used, 0) as SlotsUsed,
    coalesce(deal_summary.slots_total, 0) as SlotsTotal,
    coalesce(open_flags.flag_count, 0) as OpenFlagCount
from vep_db.account account
join cybernations_db.nation nation on account.nation_id = nation.id
left join cybernations_db.alliance alliance on nation.alliance_id = alliance.id
left join vep_db.account_deal_summary deal_summary on account.id = deal_summary.account_id
left join (
    select account_id, count(distinct deal_id) as flag_count
    from (
        select df.flagged_account_id as account_id, df.deal_id
        from vep_db.deal_flag df
        where df.resolved_on is null
        union all
        select df.innocent_account_id as account_id, df.deal_id
        from vep_db.deal_flag df
        where df.resolved_on is null
    ) flagged
    group by account_id
) open_flags on account.id = open_flags.account_id";

    public AccountsRepository(string connectionString) => _connectionString = connectionString;

    public async Task<IEnumerable<AccountDetail>> GetAccountDetailsAsync(bool includeOnHold = false)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        var query = AccountDetailQuery;

        if (!includeOnHold)
        {
            query += "\nwhere account.va <> 'H'";
        }

        return await sqlConnection.QueryAsync<AccountDetail>(query);
    }

    public async Task<AccountDetail> GetAccountByIdAsync(int accountId)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        return await sqlConnection.QueryFirstOrDefaultAsync<AccountDetail>(
            AccountDetailQuery + "\nwhere account.id = @account_id",
            new { account_id = accountId });
    }

    public async Task<AccountDetail> CreateAccountAsync(AccountCreateRequest request)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string createAccountQuery = @"
insert into account (
    nation_id,
    discord,
    discord_id,
    psw,
    va,
    fm,
    fac,
    dra,
    date_added
)
values (@nation_id, @discord, @discord_id, @psw, @va, @fm, @fac, @dra, now())";

        await sqlConnection.ExecuteAsync(createAccountQuery, new
        {
            nation_id = request.NationId,
            discord = request.Discord,
            discord_id = request.DiscordUniqueId,
            psw = request.UniqueCode,
            va = request.Role,
            fm = request.HasForeignMinistry,
            fac = request.HasFederalAidCommission,
            dra = request.HasDisasterReliefAgency
        });

        return await sqlConnection.QueryFirstAsync<AccountDetail>(
            AccountDetailQuery + "\nwhere account.nation_id = @nation_id",
            new { nation_id = request.NationId });
    }

    public async Task UpdateAccountAsync(AccountUpdateRequest account)
    {
        if (!await AccountExists(account.Id))
            throw new NotFoundException($"Unable to find an account to update by ID {account.Id}.");

        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
update vep_db.account
set
    discord = @discord,
    discord_id = @discord_id,
    psw = @unique_code,
    va = @role,
    fm = @has_fm,
    fac = @has_fac,
    dra = @has_dra
where id = @id";

        await sqlConnection.ExecuteAsync(query, new
        {
            id = account.Id,
            role = account.Role,
            unique_code = account.UniqueCode,
            discord = account.Discord,
            discord_id = account.DiscordUniqueId,
            has_fm = account.HasForeignMinistry,
            has_fac = account.HasFederalAidCommission,
            has_dra = account.HasDisasterReliefAgency
        });
    }

    public async Task RemoveAccountAsync(int accountId)
    {
        if (!await AccountExists(accountId))
            throw new NotFoundException($"Unable to find an account to delete by ID {accountId}");

        using var sqlConnection = new MySqlConnection(_connectionString);
        const string auditQuery = @"
insert into account_removed (
    `nation_id`, 
    `discord`, 
    `discord_id`, 
    `psw`, 
    `va`, 
    `fm`, 
    `fac`, 
    `dra`, 
    `removed_on`
)
select
    `nation_id`, 
    `discord`, 
    `discord_id`, 
    `psw`, 
    `va`, 
    `fm`, 
    `fac`, 
    `dra`, 
    now()
from account
where id = @id";
        await sqlConnection.ExecuteAsync(auditQuery, new { id = accountId });
        await sqlConnection.ExecuteAsync(
            "delete from account where id = @id",
            new { id = accountId });
    }

    public async Task<ProspectAccount> FindProspectAccountAsync(int nationId)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select	nation.id as NationId,
    nation.nation_name as NationName,
    nation.ruler_name as RulerName,
    alliance.name as AllianceName,
    (account.id is not null) as AccountAlreadyExists
from cybernations_db.nation nation
left join vep_db.account account on nation.id = account.nation_id
left join cybernations_db.alliance alliance on nation.alliance_id = alliance.id
where nation.id = @nation_id";

        var prospectSearchResults = await sqlConnection.QueryFirstOrDefaultAsync(query, new { nation_id = nationId });

        if (prospectSearchResults is null)
            return null;
        if (Convert.ToBoolean(prospectSearchResults.AccountAlreadyExists))
            throw new AlreadyExistsException($"An account for {prospectSearchResults.NationName} already exists.");

        return new ProspectAccount
        {
            NationId = prospectSearchResults.NationId,
            NationName = prospectSearchResults.NationName,
            RulerName = prospectSearchResults.RulerName,
            AllianceName = prospectSearchResults.AllianceName
        };
    }

    private async Task<bool> AccountExists(int accountId)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        return await sqlConnection.ExecuteScalarAsync<bool>(
            "select exists (select 1 from account where id = @id)",
            new { id = accountId });
    }

    public async Task<IEnumerable<TopScorerAccount>> GetTopScorerAccountsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        const string query = @"
select 	account.id as Id,
    nation.id as NationId,
    nation.nation_name as NationName,
    nation.ruler_name as RulerName,
    alliance.name as AllianceName,
    coalesce(outgoing_perf.times_in_list, 0) as TotalTimesInDebt,
    coalesce(outgoing_perf.average_days_until_removed, 0.0) as AvgDaysToClearDebt
from vep_db.account account
join cybernations_db.nation nation on account.nation_id = nation.id
join cybernations_db.alliance alliance on nation.alliance_id = alliance.id
left join (
    select account_id, count(1) as times_in_list, avg(days_until_removed) as average_days_until_removed
    from account_list_performance
    where list_id in (1, 3)
    group by account_id
) outgoing_perf on account.id = outgoing_perf.account_id
where account.va <> 'H'
  and coalesce(outgoing_perf.times_in_list, 0) > 0
order by AvgDaysToClearDebt asc
limit 10";

        var results = await sqlConnection.QueryAsync<TopScorerAccount>(query);
        var rankedResults = results.Select((account, index) =>
        {
            account.Rank = index + 1;
            return account;
        });

        return rankedResults;
    }
}