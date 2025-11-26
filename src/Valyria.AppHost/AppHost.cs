var builder = DistributedApplication.CreateBuilder(args);

// Get secrets from environment variables or user secrets
var vepDbConnectionString = builder.Configuration["ConnectionStrings:VepDb"]
    ?? throw new InvalidOperationException("ConnectionStrings:VepDb must be set in environment variables or user secrets");
var jwtSecret = builder.Configuration["JwtSecret"]
    ?? throw new InvalidOperationException("JwtSecret must be set in environment variables or user secrets");

// Add the API project with HTTP and HTTPS endpoints
var api = builder.AddProject<Projects.Valyria_Api>("valyria-api")
    .WithExternalHttpEndpoints()
    .WithEnvironment("ConnectionStrings__VepDb", vepDbConnectionString)
    .WithEnvironment("JwtSecret", jwtSecret);

builder.AddViteApp("valyria-ui", "../Valyria.Ui", "dev")
    .WithExternalHttpEndpoints()
    .WithNpm(installArgs: ["--legacy-peer-deps"])
    .WithEnvironment("NUXT_PUBLIC_API_URL", api.GetEndpoint("https"))
    .WaitFor(api)
    .WithReference(api);

builder.Build().Run();
