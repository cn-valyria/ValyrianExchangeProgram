using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Valyria.Infrastructure.Repositories;
using Valyria.Seeding;

// Seeds the local dev database after the data loader's first import. Run by the AppHost, never against production
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var vepDbConnectionString = builder.Configuration["VepDbConnectionString"]
    ?? throw new InvalidOperationException("VepDbConnectionString is not configured");
var devDiscordId = long.TryParse(builder.Configuration["Seed:DevDiscordId"], out var discordId) ? discordId : (long?)null;

using var host = builder.Build();
await host.StartAsync();

var logger = host.Services.GetRequiredService<ILogger<DevDataSeeder>>();
try
{
    var seeder = new DevDataSeeder(vepDbConnectionString, new DataLoaderRepository(vepDbConnectionString), logger, devDiscordId);
    await seeder.SeedAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "Seeding the local database failed");
    Environment.ExitCode = 1;
}

// Give the telemetry exporter a chance to flush the logs to the dashboard
await host.StopAsync();
