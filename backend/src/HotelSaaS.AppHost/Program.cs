var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.HotelSaaS_Api>("hotelsaas-api");

builder.AddNpmApp("frontend", "../../../frontend", "dev")
    .WithReference(api)
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithEnvironment("VITE_API_URL", api.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
