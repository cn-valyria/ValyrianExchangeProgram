var builder = DistributedApplication.CreateBuilder(args);

// Get secrets from environment variables or user secrets
var jwtSecret = builder.Configuration["JwtSecret"]
    ?? throw new InvalidOperationException("JwtSecret must be set in environment variables or user secrets");

var vepDbConnectionString = builder.AddConnectionString("VepDb");

// Add the API project with HTTP and HTTPS endpoints
var api = builder.AddProject<Projects.Valyria_Api>("valyria-api")
    .WithExternalHttpEndpoints()
    .WithReference(vepDbConnectionString)
    .WithEnvironment("JwtSecret", jwtSecret);

builder.AddViteApp("valyria-ui", "../Valyria.Ui", "dev")
    .WithExternalHttpEndpoints()
    .WithNpm(installArgs: ["--legacy-peer-deps"])
    .WithEnvironment("NUXT_PUBLIC_API_URL", api.GetEndpoint("https"))
    .WaitFor(api)
    .WithReference(api);

builder.Build().Run();
