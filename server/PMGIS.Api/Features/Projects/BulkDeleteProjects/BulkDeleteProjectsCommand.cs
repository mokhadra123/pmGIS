namespace PMGIS.Api.Features.Projects.BulkDeleteProjects;

public sealed record BulkDeleteProjectsCommand
{
    public IReadOnlyList<int> ProjectIds { get; init; } = [];
}

// Per-project outcomes, so a partial failure is visible rather than hidden behind one blanket result.
public sealed record BulkDeleteResult
{
    public required IReadOnlyList<int> Deleted { get; init; }
    public required IReadOnlyList<BulkDeleteFailure> Failed { get; init; }
}

public sealed record BulkDeleteFailure(int ProjectId, string Reason);
