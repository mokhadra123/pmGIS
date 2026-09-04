using Microsoft.AspNetCore.Mvc;

using PMGIS.Domain.Enums;
using PMGIS.Infrastructure.Projects;

namespace PMGIS.Api.Features.Projects.GetProjects;

// Query-string surface of the Projects List.
public sealed record GetProjectsQuery
{
    [FromQuery] public int? Page { get; init; }
    [FromQuery] public int? PageSize { get; init; }
    [FromQuery] public string? Sort { get; init; }
    [FromQuery] public string? Dir { get; init; }
    [FromQuery] public string? Search { get; init; }
    [FromQuery] public int[]? TypeIds { get; init; }
    [FromQuery] public string[]? Statuses { get; init; }
    [FromQuery] public DateOnly? DateFrom { get; init; }
    [FromQuery] public DateOnly? DateTo { get; init; }
    [FromQuery] public double? MinLon { get; init; }
    [FromQuery] public double? MinLat { get; init; }
    [FromQuery] public double? MaxLon { get; init; }
    [FromQuery] public double? MaxLat { get; init; }
    [FromQuery] public string? PolygonWkt { get; init; }

    public ProjectQuery ToQuery() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? 25,
        Sort = ProjectSort.Normalise(Sort),
        Descending = !string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase),
        Search = Search,
        ProjectTypeIds = TypeIds ?? [],
        Statuses = [.. (Statuses ?? [])
            .Select(s => Enum.TryParse<ProjectStatus>(s, true, out var v) ? v : (ProjectStatus?)null)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)],
        DateFrom = DateFrom,
        DateTo = DateTo,
        MinLongitude = MinLon,
        MinLatitude = MinLat,
        MaxLongitude = MaxLon,
        MaxLatitude = MaxLat,
        PolygonWkt = PolygonWkt,
    };
}
