using PMGIS.Api.Features.Lookups;
using PMGIS.Infrastructure;

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
}

app.Run();
