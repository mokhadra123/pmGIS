using PMGIS.Api.Features.Projects.Shared;
using PMGIS.Domain.Enums;

namespace PMGIS.Api.Features.Projects.CreateProject;

public sealed record CreateProjectCommand
{
    public string ProjectCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? ProjectTypeId { get; init; }
    public ProjectStatus Status { get; init; } = ProjectStatus.Draft;
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public decimal? Budget { get; init; }
    public int? OwnerUserId { get; init; }

    // Point chosen on the map.
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    public IReadOnlyList<ProjectActivityInput> Activities { get; init; } = [];

    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;
}

public sealed record CreateProjectResponse(int Id, string ProjectCode, long? ObjectId);
