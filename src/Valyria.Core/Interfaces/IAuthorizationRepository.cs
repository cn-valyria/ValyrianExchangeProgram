using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IAuthorizationRepository
{
    Task<AuthorizedAccount> Authorize(int nationId, string uniqueCode);
    Task<AuthorizedAccount> Authorize(string rulerName, string uniqueCode);
}