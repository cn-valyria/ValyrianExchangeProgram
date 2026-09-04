using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;

namespace Valyria.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var vepDbConnectionString = configuration["VepDbConnectionString"];

        return services
            .AddTransient<IAuthorizationRepository>(sp => new AuthorizationRepository(vepDbConnectionString))
            .AddTransient<IAccountsRepository>(sp => new AccountsRepository(vepDbConnectionString))
            .AddTransient<ITransactionsRepository>(sp => new TransactionsRepository(vepDbConnectionString))
            .AddTransient<IListsRepository>(sp => new ListsRepository(vepDbConnectionString))
            .AddTransient<IAccountListPerformanceRepository>(sp => new AccountListPerformanceRepository(vepDbConnectionString))
            .AddTransient<IAdminRepository>(sp => new AdminRepository(vepDbConnectionString))
            .AddTransient<IDealsRepository>(sp => new DealsRepository(vepDbConnectionString));
    }
}
