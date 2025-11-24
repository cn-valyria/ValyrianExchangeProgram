using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IAccountListPerformanceRepository
{
    Task<List<AccountListPerformance>> GetAccountListPerformanceAsync(int accountId, bool includeIncoming);
}