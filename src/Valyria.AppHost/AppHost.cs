using Microsoft.Extensions.Configuration;
using Valyria.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("env");

// Local runs get their own MySQL, migrated and seeded on startup (see README.md). Set UseProductionDb=true (or use
// the https-prod launch profile) to point at the production database instead. Publishing always uses production
var useProductionDb = builder.ExecutionContext.IsPublishMode || builder.Configuration.GetValue<bool>("UseProductionDb");

// Get secrets from environment variables or user secrets
var jwtSecret = builder.AddParameter("JwtSecret");
var vepDbConnectionString = useProductionDb ? builder.AddParameter("VepDbConnectionString") : null;
var cnDbConnectionString = useProductionDb ? builder.AddParameter("CnDbConnectionString") : null;
var discordWebhookUrl = builder.AddParameter("DiscordWebhookUrl");
// Shared with the Discord bot on Val Town (its VEP_API_KEY), see discord-bot/README.md
var discordBotApiKey = builder.AddParameter("DiscordBotApiKey", secret: true);

// Add the API project with HTTP and HTTPS endpoints
var api = builder.AddProject<Projects.Valyria_Api>("valyria-api")
    .WithExternalHttpEndpoints();
if (vepDbConnectionString is not null)
    api.WithEnvironment("VepDbConnectionString", vepDbConnectionString);
api.WithEnvironment("JwtSecret", jwtSecret)
    .WithEnvironment("DiscordBotApiKey", discordBotApiKey);

var storage = builder.AddAzureStorage("valyria-storage").RunAsEmulator();
var nationBlob = storage.AddBlobContainer("nation");
var allianceBlob = storage.AddBlobContainer("alliance");
var aidBlob = storage.AddBlobContainer("aid");
var warBlob = storage.AddBlobContainer("war");

// Add the batch job that runs on a schedule (1:30 PM UTC daily)
#pragma warning disable ASPIREAZURE002
var dataLoader = builder.AddProject<Projects.Valyria_Batch_DataLoader>("valyria-batch-dataloader")
    .WithReference(nationBlob).WaitFor(nationBlob)
    .WithReference(allianceBlob).WaitFor(allianceBlob)
    .WithReference(aidBlob).WaitFor(aidBlob)
    .WithReference(warBlob).WaitFor(warBlob);
if (vepDbConnectionString is not null && cnDbConnectionString is not null)
    dataLoader
        .WithEnvironment("VepDbConnectionString", vepDbConnectionString)
        .WithEnvironment("CybernationsDbConnectionString", cnDbConnectionString);
dataLoader
    .WithEnvironment("DiscordWebhookUrl", discordWebhookUrl)
    .PublishAsScheduledAzureContainerAppJob(cronExpression: "0/5 0-2,12-14 * * *");
#pragma warning restore ASPIREAZURE002

if (useProductionDb)
{
    // Database migrations are not an AppHost resource against production: they would migrate prod from whatever
    // branch is checked out. The deploy workflow (.github/workflows/ci-cd.yml) runs Flyway against prod right
    // before `aspire deploy`. For the same reason, a local run only imports into prod when the loader is started
    // by hand from the dashboard
    if (builder.ExecutionContext.IsRunMode)
        dataLoader.WithExplicitStart();
}
else
{
    builder.AddLocalDatabase(api, dataLoader);
}

var domainName = builder.AddParameter("customDomain");
var certificateName = builder.AddParameter("certificateName");
builder.AddViteApp("valyria-ui", "../Valyria.Ui", "dev")
    .WithExternalHttpEndpoints()
    .WithNpm(installArgs: ["--legacy-peer-deps"])
    .WithEnvironment("NUXT_PUBLIC_API_URL", api.GetEndpoint("https"))
    .WaitFor(api)
    .WithReference(api)
    .PublishAsAzureContainerApp((module, app) =>
    {
#pragma warning disable ASPIREACADOMAINS001
        app.ConfigureCustomDomain(domainName, certificateName);
#pragma warning restore ASPIREACADOMAINS001
    });

builder.Build().Run();
