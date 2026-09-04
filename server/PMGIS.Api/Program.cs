using PMGIS.Api.Features.Gis;
using PMGIS.Api.Features.Lookups;
using PMGIS.Api.Features.Projects;
using PMGIS.Infrastructure;
using PMGIS.Infrastructure.Seeding;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Health checks, OpenTelemetry, service discovery and HTTP resilience defaults.
builder.AddServiceDefaults();

// Database, ArcGIS client and the resilience pipeline in front of it.
builder.AddInfrastructure();

// Each feature registers its own handlers and validators. No assembly scanning: adding a
// slice is a visible edit in that feature's module.
builder.Services.AddLookupsFeature();
builder.Services.AddProjectsFeature();
builder.Services.AddGisFeature();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// The Angular client runs on its own origin in development.
const string ClientCors = "client";

builder.Services.AddCors(options =>
    options.AddPolicy(ClientCors, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                     ?? ["http://localhost:4200"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        // The CSV export sends its filename in this header; without exposing it the
        // browser hides it from the client.
        .WithExposedHeaders("Content-Disposition")));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors(ClientCors);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("PMGIS API")
        .WithTheme(ScalarTheme.BluePlanet)
        .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Fetch));

    // Development only and idempotent: it exits immediately if projects already exist.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
}

// The whole API surface, one line per feature.
app.MapLookupsFeature();
app.MapProjectsFeature();
app.MapGisFeature();

app.MapDefaultEndpoints();

app.Run();
