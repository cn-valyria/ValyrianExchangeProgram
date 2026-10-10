using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Valyria.Api.Auth;
using Valyria.Api.Extensions;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Extensions;
using Valyria.Infrastructure.Repositories;

namespace Valyria.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersEveryRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["VepDbConnectionString"] = "Server=localhost" })
            .Build();

        using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();

        Assert.IsType<AuthorizationRepository>(provider.GetRequiredService<IAuthorizationRepository>());
        Assert.IsType<AccountsRepository>(provider.GetRequiredService<IAccountsRepository>());
        Assert.IsType<AdminRepository>(provider.GetRequiredService<IAdminRepository>());
        Assert.IsType<DealsRepository>(provider.GetRequiredService<IDealsRepository>());
        Assert.IsType<MassMessagesRepository>(provider.GetRequiredService<IMassMessagesRepository>());
    }

    [Fact]
    public void AddAuthenticationServices_RegistersTokenAndAuthenticationProviders()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddInfrastructure(new ConfigurationBuilder().Build())
            .AddAuthenticationServices()
            .BuildServiceProvider();

        Assert.IsType<TokenProvider>(provider.GetRequiredService<ITokenProvider>());
        Assert.IsType<AuthenticationProvider>(provider.GetRequiredService<IAuthenticationProvider>());
    }
}
