using api.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services
            .AddLogging()
            .AddVepDbRepositories(context.Configuration)
            .AddInternalProviders()
            .AddAutoMapper(typeof(Program));
    })
    .Build();

host.Run();