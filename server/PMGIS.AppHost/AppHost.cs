var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 5432)
    .WithImage("postgis/postgis")
    .WithImageTag("16-3.4")
    .WithDataVolume("pmgis-pgdata")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("pmgisdb");

var api = builder.AddProject<Projects.PMGIS_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithExternalHttpEndpoints();

builder.Build().Run();
