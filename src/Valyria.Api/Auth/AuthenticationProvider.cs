using System.Threading.Tasks;
using Valyria.Core.Contracts;
using Valyria.Core.Interfaces;
using Valyria.Core.DTOs;

namespace Valyria.Api.Auth;

public class AuthenticationProvider : IAuthenticationProvider
{
    private readonly IAuthorizationRepository _authorizationRepository;
    
    public AuthenticationProvider(IAuthorizationRepository authorizationRepository)
    {
        _authorizationRepository = authorizationRepository;
    }

    public async Task<AuthorizedAccount> GetAuthorizedAccount(AuthorizeUserRequest request)
    {
        AuthorizedAccount authorizedUser = null;
        if (!RequestIsValid(request))
            return authorizedUser;

        if (!string.IsNullOrEmpty(request.NationId) && int.TryParse(request.NationId, out var nationId))
            authorizedUser = await _authorizationRepository.Authorize(nationId, request.UniqueCode);
        else
            authorizedUser = await _authorizationRepository.Authorize(request.RulerName, request.UniqueCode);

        return authorizedUser;
    }

    private bool RequestIsValid(AuthorizeUserRequest request)
    {
        if (string.IsNullOrEmpty(request.UniqueCode))
            return false;
        if (string.IsNullOrEmpty(request.NationId) && string.IsNullOrEmpty(request.RulerName))
            return false;

        return true;
    }
}

public interface IAuthenticationProvider
{
    Task<AuthorizedAccount> GetAuthorizedAccount(AuthorizeUserRequest request);
}