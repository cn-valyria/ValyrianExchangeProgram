using api.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repository;

namespace api.Utils;

internal static class StartupExtensions
{
    public static IServiceCollection AddVepDbRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        var vepDbConnectionString = configuration.GetConnectionString("VepDb");

        return services
            .AddTransient<IAuthorizationRepository>(sp => new AuthorizationRepository(vepDbConnectionString))
            .AddTransient<IAccountsRepository>(sp => new AccountsRepository(vepDbConnectionString))
            .AddTransient<ITransactionsRepository>(sp => new TransactionsRepository(vepDbConnectionString))
            .AddTransient<IListsRepository>(sp => new ListsRepository(vepDbConnectionString))
            .AddTransient<IAdminRepository>(sp => new AdminRepository(vepDbConnectionString));
    }

    public static IServiceCollection AddInternalProviders(this IServiceCollection services) => services
        .AddTransient<ITokenProvider, TokenProvider>()
        .AddTransient<IAuthenticationProvider, AuthenticationProvider>();
}