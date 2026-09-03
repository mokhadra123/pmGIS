using PMGIS.Api.Features.Lookups;
using PMGIS.Infrastructure;
using PMGIS.Infrastructure.Seeding;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.AddServiceDefaults();
builder.AddInfrastructure();

builder.Services.AddLookupsFeature();


var app = builder.Build();

app.MapLookupsFeature();

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

app.Run();
