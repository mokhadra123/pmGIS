var builder = DistributedApplication.CreateBuilder(args);

// Password read from configuration under "Parameters:postgres-password" (user secrets).
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

// The database server container, on a fixed port so the EF tooling has a stable target.
var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 5432)
    .WithImage("postgis/postgis")
    .WithImageTag("16-3.4")
    .WithDataVolume("pmgis-pgdata")
    .WithLifetime(ContainerLifetime.Persistent);

// A named database on that server. The API asks for its connection string by this name.
var database = postgres.AddDatabase("pmgisdb");

// The ASP.NET Core API.
var api = builder.AddProject<Projects.PMGIS_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithExternalHttpEndpoints();

// The Angular dev server. The path is relative to this project.
builder.AddJavaScriptApp("client", "../../client", "start")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 4200, targetPort: 4200, env: "PORT", isProxied: false)
    .WithExternalHttpEndpoints();

builder.Build().Run();
