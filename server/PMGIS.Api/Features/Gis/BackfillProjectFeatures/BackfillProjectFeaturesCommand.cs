namespace PMGIS.Api.Features.Gis.BackfillProjectFeatures;

// Creates layer points for rows that have coordinates but no ObjectId.
public sealed record BackfillProjectFeaturesCommand
{
    // Features per applyEdits request.
    public int BatchSize { get; init; } = 200;

    // Stop after this many candidates.
    public int MaxProjects { get; init; }
}

public sealed record BackfillFailure(int ProjectId, string ProjectCode, string Reason);

// What the run actually did.
public sealed record BackfillProjectFeaturesResult
{
    // Rows with a location and no ObjectId when the run started.
    public required int Candidates { get; init; }

    // Rows the run attempted, i.e.
    public required int Attempted { get; init; }

    public required int FeaturesCreated { get; init; }

    // Existing layer features re-linked rather than duplicated.
    public required int FeaturesAdopted { get; init; }

    public required int ObjectIdsWritten { get; init; }

    public required int FailureCount { get; init; }

    // Capped sample; FailureCount is the true total.
    public required IReadOnlyList<BackfillFailure> Failures { get; init; }

    public required int BatchSize { get; init; }
    public required int BatchesProcessed { get; init; }
    public required double ElapsedSeconds { get; init; }

    // True when the run stopped early because the layer kept rejecting writes.
    public bool Aborted { get; init; }
    public string? AbortReason { get; init; }
}
