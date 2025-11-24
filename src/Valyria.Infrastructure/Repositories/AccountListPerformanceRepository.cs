using Valyria.Core.Interfaces;
using Dapper;
using MySql.Data.MySqlClient;
using Valyria.Core.DTOs;

namespace Valyria.Infrastructure.Repositories;

public class AccountListPerformanceRepository : IAccountListPerformanceRepository
{
    private readonly string _connectionString;

    public AccountListPerformanceRepository(string connectionString) => _connectionString = connectionString;

    public async Task<List<AccountListPerformance>> GetAccountListPerformanceAsync(int accountId, bool includeIncoming)
    {
        using var sqlConnection = new MySqlConnection(_connectionString);

        const string GetListPerformanceQuery = @"
select 
	account.id as AccountId,
    nation.id as NationId,
    nation.ruler_name as RulerName, 
    nation.nation_name as NationName, 
    alliance.name as AllianceName,
    account.va as Role, 
    case when list_id in (1, 3) then 'Outgoing' else 'Incoming' end as ListType, 
    day_started as DayStarted, 
    days_until_removed as DaysUntilRemoved
from account
join cybernations_db.nation on account.nation_id = nation.id
join cybernations_db.alliance on nation.alliance_id = alliance.id
join account_list_performance on account.id = account_list_performance.account_id
where account.id = @accountId
and (@includeIncoming or list_id not in (2, 4))
order by day_started desc;";

        var accountListPerformances =
            await sqlConnection.QueryAsync<AccountListPerformance>(GetListPerformanceQuery, new { accountId, includeIncoming });

        return accountListPerformances.ToList();
    }
}