using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Valyria.Batch.DataLoader;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;
using Valyria.Infrastructure.Services.CnFileImport;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAutoMapper(config =>
{
    config.AddProfile<MappingProfile>();
});

// Get connection string from configuration
var vepDbConnectionString = builder.Configuration["VepDbConnectionString"]
    ?? throw new InvalidOperationException("VepDbConnectionString is not configured");
var cnDbConnectionString = builder.Configuration["CybernationsDbConnectionString"]
    ?? throw new InvalidOperationException("CybernationsDbConnectionString is not configured");

// Register CN file import services
builder.Services.AddSingleton<ICnFileGrabber, CnFileGrabber>();
builder.Services.AddSingleton<ICnFileParser, CnFileParser>();

// Register repositories
builder.Services.AddKeyedSingleton<IDataLoaderRepository>("VepDb", new DataLoaderRepository(vepDbConnectionString));
builder.Services.AddKeyedSingleton<IDataLoaderRepository>("CnDb", new DataLoaderRepository(cnDbConnectionString));

// Register the batch worker
builder.Services.AddHostedService<DataLoaderWorker>();

var host = builder.Build();

await host.RunAsync();
