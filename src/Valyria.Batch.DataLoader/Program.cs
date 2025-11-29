using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Valyria.Batch.DataLoader;
using Valyria.Core.Interfaces;
using Valyria.Infrastructure.Repositories;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Get connection string from configuration
var connectionString = builder.Configuration["VepDbConnectionString"];

// Register repositories
builder.Services.AddSingleton<IDataLoaderRepository>(new DataLoaderRepository(connectionString));

// Register the batch worker
builder.Services.AddHostedService<DataLoaderWorker>();

var host = builder.Build();

await host.RunAsync();
