var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("env");

// Get secrets from environment variables or user secrets
var jwtSecret = builder.AddParameter("JwtSecret");
var vepDbConnectionString = builder.AddParameter("VepDbConnectionString");
var cnDbConnectionString = builder.AddParameter("CnDbConnectionString");
var discordWebhookUrl = builder.AddParameter("DiscordWebhookUrl");

// Add Flyway migration resource for the database
// Note: You need to set FlywayJdbcUrl in user secrets with format: jdbc:mysql://host:port/database?user=username&password=password
var flywayJdbcUrl = builder.AddParameter("FlywayJdbcUrl");
var flyway = builder.AddFlyway("valyria-migrations", "../Valyria.Database/migrations")
    .WithArgs("-url", flywayJdbcUrl, "migrate");

// Add the API project with HTTP and HTTPS endpoints
var api = builder.AddProject<Projects.Valyria_Api>("valyria-api")
    .WithExternalHttpEndpoints()
    .WithEnvironment("VepDbConnectionString", vepDbConnectionString)
    .WithEnvironment("JwtSecret", jwtSecret)
    .WaitFor(flyway);

var storage = builder.AddAzureStorage("valyria-storage").RunAsEmulator();
var nationBlob = storage.AddBlobContainer("nation");
var allianceBlob = storage.AddBlobContainer("alliance");
var aidBlob = storage.AddBlobContainer("aid");
var warBlob = storage.AddBlobContainer("war");

// Add the batch job that runs on a schedule (1:30 PM UTC daily)
#pragma warning disable ASPIREAZURE002
builder.AddProject<Projects.Valyria_Batch_DataLoader>("valyria-batch-dataloader")
    .WithReference(nationBlob).WaitFor(nationBlob)
    .WithReference(allianceBlob).WaitFor(allianceBlob)
    .WithReference(aidBlob).WaitFor(aidBlob)
    .WithReference(warBlob).WaitFor(warBlob)
    .WithEnvironment("VepDbConnectionString", vepDbConnectionString)
    .WithEnvironment("CybernationsDbConnectionString", cnDbConnectionString)
    .WithEnvironment("DiscordWebhookUrl", discordWebhookUrl)
    .PublishAsScheduledAzureContainerAppJob(cronExpression: "0/5 0-2,12-14 * * *");
#pragma warning restore ASPIREAZURE002

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
