var builder = DistributedApplication.CreateBuilder(args);

// Get secrets from environment variables or user secrets
var jwtSecret = builder.AddParameter("JwtSecret");
var vepDbConnectionString = builder.AddConnectionString("VepDb");

// Add the API project with HTTP and HTTPS endpoints
var api = builder.AddProject<Projects.Valyria_Api>("valyria-api")
    .WithExternalHttpEndpoints()
    .WithEnvironment("ConnectionStrings:VepDb", vepDbConnectionString)
    .WithEnvironment("JwtSecret", jwtSecret);

builder.AddViteApp("valyria-ui", "../Valyria.Ui", "dev")
    .WithExternalHttpEndpoints()
    .WithNpm(installArgs: ["--legacy-peer-deps"])
    .WithEnvironment("NUXT_PUBLIC_API_URL", api.GetEndpoint("https"))
    .WaitFor(api)
    .WithReference(api);

builder.Build().Run();
