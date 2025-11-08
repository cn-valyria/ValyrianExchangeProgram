using Valyria.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;
using Valyria.Core.DTOs;

namespace Valyria.Infrastructure.Repositories;

public class AuthorizationRepository : IAuthorizationRepository
{
    private readonly string _connectionString;

    private const string BaseGetAuthorizedUserQuery = @"
select account.id as AccountId
from vep_db.account
join cybernations_db.nation on account.nation_id = nation.id";

    public AuthorizationRepository(string connectionString) => _connectionString = connectionString;

    public async Task<AuthorizedAccount> Authorize(int nationId, string uniqueCode)
    {
        using var connection = new MySqlConnection(_connectionString);

        var account = await connection.QueryFirstOrDefaultAsync<AuthorizedAccount>(
            BaseGetAuthorizedUserQuery + " where nation_id = @nationId and psw = @uniqueCode",
            new { nationId, uniqueCode });

        account.Roles = await GetAccountRolesAsync(account.AccountId);

        return account;
    }

    public async Task<AuthorizedAccount> Authorize(string rulerName, string uniqueCode)
    {
        using var connection = new MySqlConnection(_connectionString);

        var account = await connection.QueryFirstOrDefaultAsync<AuthorizedAccount>(
            BaseGetAuthorizedUserQuery + " where ruler_name = @rulerName and psw = @uniqueCode",
            new { rulerName, uniqueCode });

        account.Roles = await GetAccountRolesAsync(account.AccountId);

        return account;
    }

    private async Task<IEnumerable<AccountRole>> GetAccountRolesAsync(int accountId)
    {
        using var connection = new MySqlConnection(_connectionString);
        const string getAccountRolesQuery = @"
select role.id as Id, name as Name
from role 
join account_role on role.id = account_role.role_id
join account on account_role.account_id = account.id
where account.id = @accountId";

        return await connection.QueryAsync<AccountRole>(getAccountRolesQuery, new { accountId });
    }
}