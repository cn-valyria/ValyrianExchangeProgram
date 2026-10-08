using System.Collections.Generic;
using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IAdminRepository
{
    Task<GeneralProgramSummary> GetGeneralProgramSummaryAsync();
    Task<SlotUsageSummary> GetSlotUsageSummaryAsync();
    Task<DealHealthSummary> GetDealHealthSummaryAsync();
    Task<AidRatioSummary> GetAidRatioSummaryAsync();
    Task<List<DealAidCountByDay>> GetDealAidPerDayAsync();
    Task<List<AccountCountByRole>> GetAccountsPerRoleAsync();
    Task<List<TotalSlotGroupsByAlliance>> GetTotalSlotsPerAllianceAsync();
    Task<List<OpenSlotCountByRole>> GetOpenSlotsPerRoleAsync();
    Task<List<AccountCountByAlliance>> GetAccountsPerAllianceAsync();
    Task<List<AdminAccessSetting>> GetAdminAccessSettingsAsync();
    Task<bool> CreateAdminAccessAsync(int accountId, int adminRoleId);
    Task<bool> DeleteAdminAccessAsync(int accountId, int adminRoleId);
}