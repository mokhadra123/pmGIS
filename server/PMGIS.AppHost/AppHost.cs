var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
  .WithImage("postgis/postgis")
  .WithImageTag("16-3.4")
  .WithDataVolume("pmgis-pgdata")
  .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("pmgisdb");

builder.Build().Run();
