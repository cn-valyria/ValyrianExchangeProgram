using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Valyria.Batch.DataLoader;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;
using Valyria.Infrastructure.Services.CnFileImport;
using Valyria.Infrastructure.Services.Discord;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Configure Mapster mappings
MappingProfile.ConfigureMappings();

// Get connection string from configuration
var vepDbConnectionString = builder.Configuration["VepDbConnectionString"]
    ?? throw new InvalidOperationException("VepDbConnectionString is not configured");
var cnDbConnectionString = builder.Configuration["CybernationsDbConnectionString"]
    ?? throw new InvalidOperationException("CybernationsDbConnectionString is not configured");
// Without a webhook URL the Discord pings are skipped (logged as a warning), which is fine for local runs
var discordWebhookUrl = builder.Configuration["DiscordWebhookUrl"] ?? string.Empty;

// Register CN file import services. Local runs can serve a saved set of files instead of downloading
// today's (CnFileSource=Local, set by the AppHost), for working offline or when CN is down
if (string.Equals(builder.Configuration["CnFileSource"], "Local", StringComparison.OrdinalIgnoreCase))
{
    var savedFilesPath = builder.Configuration["CnSavedFilesPath"]
        ?? throw new InvalidOperationException("CnSavedFilesPath is not configured");
    builder.Services.AddSingleton<ICnFileGrabber>(services =>
        new LocalCnFileGrabber(savedFilesPath, services.GetRequiredService<ILogger<LocalCnFileGrabber>>()));
}
else
{
    builder.Services.AddSingleton<ICnFileGrabber, CnFileGrabber>();
}
builder.Services.AddSingleton<ICnFileParser, CnFileParser>();

// Register repositories
builder.Services.AddKeyedSingleton<IDataLoaderRepository>("VepDb", new DataLoaderRepository(vepDbConnectionString));
builder.Services.AddKeyedSingleton<IDataLoaderRepository>("CnDb", new DataLoaderRepository(cnDbConnectionString));
builder.Services.AddSingleton<IMassMessagesRepository>(new MassMessagesRepository(vepDbConnectionString));

// Register Discord webhook service
builder.Services.Configure<DiscordWebhookOptions>((options) => options.WebhookUrl = discordWebhookUrl);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDiscordWebhookService, DiscordWebhookService>();

// Register blob storage
builder.AddKeyedAzureBlobContainerClient("nation");
builder.AddKeyedAzureBlobContainerClient("alliance");
builder.AddKeyedAzureBlobContainerClient("aid");
builder.AddKeyedAzureBlobContainerClient("war");

// Register the batch worker
builder.Services.AddHostedService<DataLoaderWorker>();

var host = builder.Build();

await host.RunAsync();
