using System.Collections.Generic;
using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IAdminRepository
{
    Task<ProgramSummary> GetProgramSummaryAsync();
    Task<List<SlotConfigSummary>> GetSlotConfigsAsync();
    Task<List<DealHealthByPairing>> GetDealHealthAsync();
    Task<List<DailyDealActivity>> GetDealActivityAsync(int days);
    Task<List<CancellationReasonCount>> GetCancellationReasonsAsync(int days);
    Task<List<AdminAccessSetting>> GetAdminAccessSettingsAsync();
    Task<bool> CreateAdminAccessAsync(int accountId, int adminRoleId);
    Task<bool> DeleteAdminAccessAsync(int accountId, int adminRoleId);
}
