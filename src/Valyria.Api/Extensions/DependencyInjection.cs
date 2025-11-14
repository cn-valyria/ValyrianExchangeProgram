using Microsoft.Extensions.DependencyInjection;
using Valyria.Api.Auth;

namespace Valyria.Api.Extensions;

public static class DependencyInjection
{
    /// <summary>
    /// Add authentication and authorization services for the application.
    /// This includes token generation and user authentication via the AuthorizationRepository.
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        return services
            .AddTransient<ITokenProvider, TokenProvider>()
            .AddTransient<IAuthenticationProvider, AuthenticationProvider>();
    }
}
