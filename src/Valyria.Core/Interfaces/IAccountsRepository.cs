using System.Collections.Generic;
using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IAccountsRepository
{
    Task<IEnumerable<AccountDetail>> GetAccountDetailsAsync(bool includeOnHold = false);

    Task<AccountDetail> GetAccountByIdAsync(int accountId);

    Task<AccountDetail> CreateAccountAsync(AccountCreateRequest request);

    Task UpdateAccountAsync(AccountUpdateRequest account);

    Task RemoveAccountAsync(int accountId);

    Task<ProspectAccount> FindProspectAccountAsync(int nationId);

    Task<IEnumerable<TopScorerAccount>> GetTopScorerAccountsAsync();
}