using System.Collections.Generic;
using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IAdminRepository
{
    Task<GeneralProgramSummary> GetGeneralProgramSummaryAsync();
    Task<SlotUsageSummary> GetSlotUsageSummaryAsync();
    Task<UnusedCreditSummary> GetUnusedCreditSummaryAsync();
    Task<AidRatioSummary> GetAidRatioSummaryAsync();
    Task<List<TransactionCountByDay>> GetTransactionsPerDayAsync();
    Task<List<CreditGroupsByAlliance>> GetPendingCreditPerAllianceAsync();
    Task<List<AccountCountByRole>> GetAccountsPerRoleAsync();
    Task<List<TotalSlotGroupsByAlliance>> GetTotalSlotsPerAllianceAsync();
    Task<List<FreeSlotCountByList>> GetFreeSlotsPerListAsync();
    Task<List<AccountCountByAlliance>> GetAccountsPerAllianceAsync();
    Task<List<AdminAccessSetting>> GetAdminAccessSettingsAsync();
    Task<bool> CreateAdminAccessAsync(int accountId, int adminRoleId);
    Task<bool> DeleteAdminAccessAsync(int accountId, int adminRoleId);
}