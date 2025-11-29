var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("env");

// Get secrets from environment variables or user secrets
var jwtSecret = builder.AddParameter("JwtSecret");
var vepDbConnectionString = builder.AddParameter("VepDbConnectionString");

// Add the API project with HTTP and HTTPS endpoints
var api = builder.AddProject<Projects.Valyria_Api>("valyria-api")
    .WithExternalHttpEndpoints()
    .WithEnvironment("VepDbConnectionString", vepDbConnectionString)
    .WithEnvironment("JwtSecret", jwtSecret);

// Add the batch job that runs on a schedule (1:30 PM UTC daily)
#pragma warning disable ASPIREAZURE002
builder.AddProject<Projects.Valyria_Batch_DataLoader>("valyria-batch-dataloader")
    .WithEnvironment("VepDbConnectionString", vepDbConnectionString)
    .PublishAsScheduledAzureContainerAppJob(cronExpression: "0/5 0-2,12-14 * * *");
#pragma warning restore ASPIREAZURE002

builder.AddViteApp("valyria-ui", "../Valyria.Ui", "dev")
    .WithExternalHttpEndpoints()
    .WithNpm(installArgs: ["--legacy-peer-deps"])
    .WithEnvironment("NUXT_PUBLIC_API_URL", api.GetEndpoint("https"))
    .WaitFor(api)
    .WithReference(api)
    .PublishAsDockerFile();

builder.Build().Run();
