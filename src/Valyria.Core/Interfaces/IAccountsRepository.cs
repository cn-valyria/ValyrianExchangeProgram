using System.Collections.Generic;
using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IAccountsRepository
{
    Task<IEnumerable<AccountDetail>> GetAccountDetailsAsync(bool includeOnHold = false);

    /// <summary>
    /// Returns null if the account does not exist
    /// </summary>
    Task<AccountDetail> GetAccountByIdAsync(int accountId);

    Task<AccountDetail> CreateAccountAsync(AccountCreateRequest request);

    Task UpdateAccountAsync(AccountUpdateRequest account);

    /// <summary>
    /// Cancels the account's active deals and moves it to the 'H' (hold) role, so it isn't matched
    /// </summary>
    Task HoldAccountAsync(int accountId, string heldBy);

    Task RemoveAccountAsync(int accountId);

    Task<ProspectAccount> FindProspectAccountAsync(int nationId);

    Task<IEnumerable<TopScorerAccount>> GetTopScorerAccountsAsync();
}