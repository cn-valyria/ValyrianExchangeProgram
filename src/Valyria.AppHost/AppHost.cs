var builder = DistributedApplication.CreateBuilder(args);

// Add the API project with HTTP and HTTPS endpoints
var api = builder.AddProject<Projects.Valyria_Api>("valyria-api")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithHttpsEndpoint(port: 5001, name: "https")
    .WithExternalHttpEndpoints();

builder.Build().Run();
