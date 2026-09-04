using PMGIS.Domain.Enums;

namespace PMGIS.Api.Features.Projects.Shared;

// One activity row as the form posts it.
public sealed record ProjectActivityInput
{
    // Null for a new row; an existing id for a row being changed.
    public int? Id { get; init; }

    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public ActivityStatus Status { get; init; } = ActivityStatus.Planned;
    public int AssignedToUserId { get; init; }
    public int PercentComplete { get; init; }
}
