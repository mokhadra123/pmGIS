using PMGIS.Api.Features.Projects.Shared;
using PMGIS.Domain.Enums;

namespace PMGIS.Api.Features.Projects.UpdateProject;

// The id comes from the route; a missing project is a 404, not a validation error.
public sealed record UpdateProjectCommand
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
}

public sealed record UpdateProjectResponse(int Id, string ProjectCode, long? ObjectId);
