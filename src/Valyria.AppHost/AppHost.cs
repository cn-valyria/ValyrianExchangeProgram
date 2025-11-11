var builder = DistributedApplication.CreateBuilder(args);

// Add the API project with HTTP and HTTPS endpoints
var api = builder.AddProject<Projects.Valyria_Api>("valyria-api")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithExternalHttpEndpoints();

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
