using System.Collections.Generic;
using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IAdminRepository
{
    Task<ProgramSummary> GetProgramSummaryAsync();
    Task<List<SlotConfigSummary>> GetSlotConfigsAsync();
    Task<List<DealHealthByPairing>> GetDealHealthAsync();
    Task<List<AllianceSlotConfigSummary>> GetAllianceSlotConfigsAsync();
    Task<List<AllianceDealHealth>> GetAllianceDealHealthAsync();
    Task<List<DailyDealActivity>> GetDealActivityAsync(int days);
    Task<List<CancellationReasonCount>> GetCancellationReasonsAsync(int days);
    Task<List<AdminAccessSetting>> GetAdminAccessSettingsAsync();
    Task<bool> CreateAdminAccessAsync(int accountId, int adminRoleId);
    Task<bool> DeleteAdminAccessAsync(int accountId, int adminRoleId);

    /// <summary>
    /// Every team event that hasn't been deleted, newest first
    /// </summary>
    Task<List<TeamEvent>> GetTeamEventsAsync();

    /// <summary>
    /// Record a team's +1 aid slot event and give the team's accounts their bonus slot
    /// </summary>
    Task<TeamEventCreateResult> CreateTeamEventAsync(TeamEventCreateRequest request, string createdBy);

    /// <summary>
    /// Delete a team event entered by mistake, cancelling deals on its bonus slots. Returns false if it doesn't
    /// exist or is already deleted
    /// </summary>
    Task<bool> DeleteTeamEventAsync(int teamEventId, string deletedBy);
}
