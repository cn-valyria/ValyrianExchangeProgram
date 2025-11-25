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

builder.AddNpmApp("valyria-ui", "../Valyria.Ui", "dev")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .WithNpmPackageInstallation(configureInstaller: configureInstaller =>
    {
        configureInstaller.WithArgs("--legacy-peer-deps");
    })
    .WithEnvironment("NUXT_PUBLIC_API_URL", api.GetEndpoint("http"))
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
