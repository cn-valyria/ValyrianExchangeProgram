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
    aid_activity.slots_used as SlotsUsed,
    aid_activity.slots_full as SlotsFull,
    coalesce(sent_aid.money, 0) div 1000000 as TotalCashSent,
    coalesce(recd_aid.money, 0) div 1000000 as TotalCashReceived,
    coalesce(sent_aid.technology, 0) as TotalTechSent,
    coalesce(recd_aid.technology, 0) as TotalTechReceived,
    balance.Credit,
    balance.Debt,
    balance.ctB as CashSentTechCredit,
    balance.ctS as CashReceivedTechCredit,
    balance.ccB as CashSentCashCredit,
    balance.ccS as CashReceivedCashCredit,
    balance.tcB as TechSentCashCredit,
    balance.tcS as TechReceivedCashCredit,
    balance.ttB as TechSentTechCredit,
    balance.ttS as TechReceivedTechCredit,
    999 as PreviousListOrder
from vep_db.account account
join vep_db.balance balance on account.nation_id = balance.nation_id
join vep_db.aid_activity on account.id = aid_activity.account_id
join cybernations_db.nation nation on account.nation_id = nation.id
join cybernations_db.alliance alliance on nation.alliance_id = alliance.id
left join (
	select sending_nation_id as nation_id, sum(money) as money, sum(technology) as technology
	from cybernations_db.aid
	group by sending_nation_id
) sent_aid on nation.id = sent_aid.nation_id
left join (
	select receiving_nation_id as nation_id, sum(money) as money, sum(technology) as technology
	from cybernations_db.aid
	group by receiving_nation_id
) recd_aid on nation.id = recd_aid.nation_id";

    public AccountsRepository(string connectionString) => _connectionString = connectionString;

    public async Task<IEnumerable<AccountDetail>> GetAccountDetailsAsync()
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        return await sqlConnection.QueryAsync<AccountDetail>(AccountDetailQuery);
    }

    public async Task<AccountDetail> GetAccountByIdAsync(int accountId)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);
        return await sqlConnection.QueryFirstAsync<AccountDetail>(
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
        var accountExists = await sqlConnection.QueryFirstAsync<bool?>(
            "select 1 from account where id = @id limit 1",
            new { id = accountId });

        return accountExists ?? false;
    }
}