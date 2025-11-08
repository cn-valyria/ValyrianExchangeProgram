using Microsoft.Extensions.DependencyInjection;
using Valyria.Api.Auth;

namespace Valyria.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        return services
            .AddTransient<ITokenProvider, TokenProvider>()
            .AddTransient<IAuthenticationProvider, AuthenticationProvider>();
    }
}
