using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
var discordWebhookUrl = builder.Configuration["DiscordWebhookUrl"]
    ?? throw new InvalidOperationException("DiscordWebhookUrl is not configured");

// Register CN file import services
builder.Services.AddSingleton<ICnFileGrabber, CnFileGrabber>();
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
