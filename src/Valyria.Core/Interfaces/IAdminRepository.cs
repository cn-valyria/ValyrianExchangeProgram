using System.Collections.Generic;
using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IAdminRepository
{
    Task<AdminDashboard> GetAdminDashboardAsync();
    Task<List<AdminAccessSetting>> GetAdminAccessSettingsAsync();
    Task<bool> CreateAdminAccessAsync(int accountId, int adminRoleId);
    Task<bool> DeleteAdminAccessAsync(int accountId, int adminRoleId);
}