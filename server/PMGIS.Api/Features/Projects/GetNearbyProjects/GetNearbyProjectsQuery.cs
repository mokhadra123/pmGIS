using Microsoft.AspNetCore.Mvc;

namespace PMGIS.Api.Features.Projects.GetNearbyProjects;

public sealed record GetNearbyProjectsQuery
{
    [FromQuery] public double Latitude { get; init; }
    [FromQuery] public double Longitude { get; init; }
    [FromQuery] public double RadiusKm { get; init; }
    [FromQuery] public int? Limit { get; init; }

    public int EffectiveLimit => Limit ?? 100;
}
